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
        {
            return HandleMethodCall(astCall);
        }

        var target = new ExpressionDispatcher(_ctx).EmitExpression(astCall.CallableName);
        if (target.Type is not CallableType callableType)
        {
            return new BifyTypeError("Can't call  " + astCall.CallableName.Token.Value).Throw<TypedValue>();
        }


        var args = ResolveArgs(_ctx, astCall, callableType);
        BifyDebug.Log(target.ToString());

        var callReturn = _ctx.Builder.BuildCall2(callableType.Type, target.Value, args.Select(i => i.Value).ToArray());
        return new TypedValue(callReturn, callableType.ReturnType);
    }

    private TypedValue HandleMethodCall(AstCall astCall)
    {
        var targetNode = astCall.CallableName;
        if (targetNode is AstMemberAccess memberAccess)
        {
            var left = new ExpressionDispatcher(_ctx).EmitExpression(memberAccess.Left);
            if (targetNode.Right is not AstIdentifier id)
                return new BifyAttributeError($"Invalid member identifier.").Throw<TypedValue>();

            if (left.Type is not ClassType classType)
                return new BifyTypeError($"Cannot call member on non-class type '{left.Type.Name}'")
                    .Throw<TypedValue>();
            
            if (!classType.Methods.TryGetValue(id.Name, out var method))
                return new BifyAttributeError($"Method '{id.Name}' does not exist").Throw<TypedValue>();
            
            MemberResolver.CheckAccess(_ctx, method.DeclaringClass, method.AccessLevel, method.Name);
            var thisReference = UpgradeWeakReference(left);
            LLVMValueRef callee;
            if (method.VTableIndex >= 0)
            {
                var ptrType = LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0);
                var vptrSlot = _ctx.Builder.BuildStructGEP2(classType.StructType, thisReference.Value, 0, "vptr_slot");
                var vtable = _ctx.Builder.BuildLoad2(ptrType, vptrSlot, "vtable");
                var slotPtr = _ctx.Builder.BuildGEP2(ptrType, vtable,
                    new[] { LLVMValueRef.CreateConstInt(_ctx.Context.Int32Type, (ulong)method.VTableIndex, false) },
                    "slot_ptr");
                callee = _ctx.Builder.BuildLoad2(ptrType, slotPtr, $"{method.Name}_fn");
            }
            else
            {
                callee = _ctx.Module.GetNamedFunction(method.Signature.FullName);
                if (callee.Handle == IntPtr.Zero)
                    throw new Exception($"Method {method.Signature.FullName} not found");
            }

            var args = ResolveArgs(_ctx, astCall, method.Signature);

            var callReturn =
                _ctx.Builder.BuildCall2(method.Signature.Type, callee,
                    [thisReference.Value, .. args.Select(i => i.Value)]);
            return new TypedValue(callReturn, method.Signature.ReturnType);
        }
        else
        {
            throw new Exception("Target node is not member accesss");
        }
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

    private static TypedValue[] ResolveArgs(
        CodeGenContext ctx,
        AstCall astCall,
        CallableType callableType
    )
    {
        List<AstNode> flatArgs = [];
        ParameterResolver.FlattenComma(astCall.ArgumentsNode, flatArgs);

        ValidateArgCount(callableType, flatArgs.Count, 0);

        var args = new TypedValue[flatArgs.Count];
        var dispatcher = new ExpressionDispatcher(ctx);
        for (int i = 0; i < flatArgs.Count; i++)
        {
            args[i] = ResolveSingleArg(ctx, dispatcher, flatArgs[i], callableType, i);
        }

        return args;
    }

    private static void ValidateArgCount(CallableType callable, int provided, int offset)
    {
        int expected = callable.ParametersType.Length - offset;
        bool valid = callable.Type.IsFunctionVarArg
            ? provided >= expected
            : provided == expected;

        if (!valid)
        {
            string prefix = callable.Type.IsFunctionVarArg ? "at least " : "";
            new BifyTypeError($"Function expects {prefix}{expected} arguments, but got {provided}").Throw();
        }
    }

    private static TypedValue ResolveSingleArg(
        CodeGenContext ctx,
        ExpressionDispatcher dispatcher,
        AstNode argNode,
        CallableType callable,
        int paramIndex)
    {
        var val = dispatcher.EmitExpression(argNode);

        if (paramIndex >= callable.ParametersType.Length)
        {
            return val;
        }

        var param = callable.ParametersType[paramIndex];
        if (!TypeCaster.CanImplicitCast(val.Type, param.Type))
        {
            new BifyTypeError(
                $"Argument '{param.Name}' expects type '{param.Type}', but got '{val.Type}'"
            ).Throw();
        }

        return TypeCaster.ImplicitCast(ctx, val, param.Type);
    }
}