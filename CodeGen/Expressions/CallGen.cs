#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Emitters;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class CallGen(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public TypedValue EmitCall(AstCall astCall)
    {
        if (astCall.CallableName is AstMemberAccess)
            return HandleMethodCall(astCall);

        var target = new ExpressionDispatcher(_ctx).EmitExpression(astCall.CallableName);
        if (target.Type is not CallableType callableType)
            return Fail<TypedValue>("Can't call  " + astCall.CallableName.Token.Value, DiagnosticKind.Type, astCall);

        var args = ResolveArgs(astCall, callableType);
        BifyDebug.Log(target.ToString());

        var callReturn = _ctx.Builder.BuildCall2(callableType.Type, target.Value, args.Select(i => i.Value).ToArray());
        return new TypedValue(callReturn, callableType.ReturnType);
    }

    private TypedValue HandleMethodCall(AstCall astCall)
    {
        if (astCall.CallableName is not AstMemberAccess memberAccess)
            throw new Exception("Target node is not member accesss");

        var left = new ExpressionDispatcher(_ctx).EmitExpression(memberAccess.Left);
        if (memberAccess.Right is not AstIdentifier id)
            return Fail<TypedValue>("Invalid member identifier.", DiagnosticKind.Attribute, astCall);

        if (left.Type is not ClassType classType)
            return Fail<TypedValue>($"Cannot call member on non-class type '{left.Type.Name}'", DiagnosticKind.Type, astCall);

        if (!classType.Methods.TryGetValue(id.Name, out var method))
            return Fail<TypedValue>($"Method '{id.Name}' does not exist", DiagnosticKind.Attribute, astCall);

        if (method.IsPoison)
            return AbortPoison<TypedValue>();

        MemberResolver.CheckAccess(_ctx, method.DeclaringClass, method.AccessLevel, method.Name);
        var thisReference = UpgradeWeakReference(left);
        var callee = ResolveMethodCallee(classType, thisReference, method);
        var args = ResolveArgs(astCall, method.Signature);

        var callReturn = _ctx.Builder.BuildCall2(method.Signature.Type, callee, [thisReference.Value, .. args.Select(i => i.Value)]);
        return new TypedValue(callReturn, method.Signature.ReturnType);
    }

    private LLVMValueRef ResolveMethodCallee(ClassType classType, TypedValue thisReference, ClassMethod method)
    {
        if (method.VTableIndex >= 0)
        {
            var ptrType = LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0);
            var vptrSlot = _ctx.Builder.BuildStructGEP2(classType.StructType, thisReference.Value, 0, "vptr_slot");
            var vtable = _ctx.Builder.BuildLoad2(ptrType, vptrSlot, "vtable");
            LLVMValueRef[] indices = [LLVMValueRef.CreateConstInt(_ctx.Context.Int32Type, (ulong)method.VTableIndex, false)];
            var slotPtr = _ctx.Builder.BuildGEP2(ptrType, vtable, indices, "slot_ptr");
            return _ctx.Builder.BuildLoad2(ptrType, slotPtr, $"{method.Name}_fn");
        }

        var callee = _ctx.Module.GetNamedFunction(method.Signature.FullName);
        if (callee.Handle == IntPtr.Zero)
            throw new Exception($"Method {method.Signature.FullName} not found");

        return callee;
    }

    private TypedValue UpgradeWeakReference(TypedValue reference)
    {
        if (reference.IsWeak)
        {
            var upgraded = _ctx.HeapControl.BuildUpgradeWeak(reference.Value);
            reference = new TypedValue(upgraded, reference.Type, false);
            _ctx.HeapControl.TrackTemp(reference);
        }

        return reference;
    }

    private TypedValue[] ResolveArgs(AstCall astCall, CallableType callableType)
    {
        List<AstNode> flatArgs = [];
        ParameterResolver.FlattenComma(astCall.ArgumentsNode, flatArgs);

        ValidateArgCount(callableType, flatArgs.Count, 0, astCall);

        var args = new TypedValue[flatArgs.Count];
        var dispatcher = new ExpressionDispatcher(_ctx);
        for (int i = 0; i < flatArgs.Count; i++)
            args[i] = ResolveSingleArg(dispatcher, flatArgs[i], callableType, i);

        return args;
    }

    private void ValidateArgCount(CallableType callable, int provided, int offset, AstCall astCall)
    {
        int expected = callable.ParametersType.Length - offset;
        bool valid = callable.Type.IsFunctionVarArg
            ? provided >= expected
            : provided == expected;

        if (!valid)
        {
            string prefix = callable.Type.IsFunctionVarArg ? "at least " : "";
            Fail($"Function expects {prefix}{expected} arguments, but got {provided}", DiagnosticKind.Type, astCall);
        }
    }

    private TypedValue ResolveSingleArg(ExpressionDispatcher dispatcher, AstNode argNode, CallableType callable, int paramIndex)
    {
        var val = dispatcher.EmitExpression(argNode);

        if (paramIndex >= callable.ParametersType.Length)
            return val;

        var param = callable.ParametersType[paramIndex];
        if (!TypeCaster.CanImplicitCast(val.Type, param.Type))
            Fail($"Argument '{param.Name}' expects type '{param.Type}', but got '{val.Type}'", DiagnosticKind.Type, argNode);

        return TypeCaster.ImplicitCast(_ctx, val, param.Type);
    }
}