#nullable enable
using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class CallGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public TypedValue EmitCall(AstCall astCall)
    {
        LLVMValueRef callee;
        LLVMTypeRef funcType;
        string retName;
        LLVMValueRef? thisArg = null;

        if (astCall.CallableName is AstMemberAccess memberAccess)
        {
            var target = new ExpressionDispatcher(_ctx).EmitExpression(memberAccess.Left);
            if (target.Type is not ClassType classType)
                return new BifyTypeError($"Cannot call member on non-class type '{target.Type.Name}'").Throw<TypedValue>();

            if (memberAccess.Right is not AstIdentifier id || !classType.Fields.TryGetValue(id.Name, out var method) || !method.IsMethod)
                return new BifyAttributeError($"Type '{target.Type.Name}' has no method '{memberAccess.Right?.Token.Value}'").Throw<TypedValue>();

            MemberAccessGen.CheckAccess(_ctx, classType, method);
            funcType = method.Type.Type;
            retName = method.Type.Name;
            thisArg = target.Value;

            if (method.VTableIndex >= 0)
            {
                var ptrType = LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0);
                var vptrSlot = _ctx.Builder.BuildStructGEP2(classType.StructType, target.Value, 0, "vptr_slot");
                var vtable = _ctx.Builder.BuildLoad2(ptrType, vptrSlot, "vtable");
                var slotPtr = _ctx.Builder.BuildGEP2(ptrType, vtable, new[] { LLVMValueRef.CreateConstInt(_ctx.Context.Int32Type, (ulong)method.VTableIndex, false) }, "slot_ptr");
                callee = _ctx.Builder.BuildLoad2(ptrType, slotPtr, $"{method.Name}_fn");
            }
            else
            {
                callee = method.Value;
            }
        }
        else if (astCall.CallableName is AstIdentifier id &&
                 !_ctx.CurrentScope.TryLookup(id.Name, out _) &&
                 _ctx.Module.GetNamedFunction(id.Name).Handle == IntPtr.Zero &&
                 _ctx.CurrentScope.TryLookup("this", out var thisSym) &&
                 thisSym.Type is ClassType ct &&
                 ct.Fields.TryGetValue(id.Name, out var m) && m.IsMethod)
        {
            MemberAccessGen.CheckAccess(_ctx, ct, m);
            funcType = m.Type.Type;
            retName = m.Type.Name;
            var thisVal = _ctx.Builder.BuildLoad2(thisSym.Type.Type, thisSym.Pointer, "this");
            thisArg = thisVal;

            if (m.VTableIndex >= 0)
            {
                var ptrType = LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0);
                var vptrSlot = _ctx.Builder.BuildStructGEP2(ct.StructType, thisVal, 0, "vptr_slot");
                var vtable = _ctx.Builder.BuildLoad2(ptrType, vptrSlot, "vtable");
                var slotPtr = _ctx.Builder.BuildGEP2(ptrType, vtable, new[] { LLVMValueRef.CreateConstInt(_ctx.Context.Int32Type, (ulong)m.VTableIndex, false) }, "slot_ptr");
                callee = _ctx.Builder.BuildLoad2(ptrType, slotPtr, $"{m.Name}_fn");
            }
            else
            {
                callee = m.Value;
            }
        }
        else
        {
            (callee, funcType, retName) = ResolveCallable(astCall.CallableName);
        }

        var args = ResolveArgs(_ctx, astCall.ArgumentsNode, funcType, thisArg);
        var callReturn = _ctx.Builder.BuildCall2(funcType, callee, [.. args]);
        return new TypedValue(callReturn, new BifyType(retName, funcType.ReturnType));
    }

    private unsafe (LLVMValueRef Callee, LLVMTypeRef FuncType, string RetName) ResolveCallable(AstNode node)
    {
        if (node is AstIdentifier id)
        {
            if (_ctx.CurrentScope.TryLookup(id.Name, out var symbol) && symbol.Type.Type.Kind == LLVMTypeKind.LLVMFunctionTypeKind)
                return (symbol.Pointer, symbol.Type.Type, symbol.Type.Name);

            var func = _ctx.Module.GetNamedFunction(id.Name);
            if (func.Handle != IntPtr.Zero)
            {
                LLVMTypeRef fType = LLVM.GlobalGetValueType(func);
                return (func, fType, ToBifyType(fType.ReturnType).Name);
            }

            new BifyNameError($"Function '{id.Name}' is not defined").Throw();
        }

        var callable = new ExpressionDispatcher(_ctx).EmitExpression(node);
        return (callable.Value, callable.Type.Type, callable.Type.Name);
    }

    public static List<LLVMValueRef> ResolveArgs(CodeGenContext ctx, AstNode? node, LLVMTypeRef funcType, LLVMValueRef? thisArg = null)
    {
        var nodes = new List<AstNode>();
        FlattenComma(node, nodes);

        var paramTypes = funcType.GetParamTypes();
        int offset = thisArg.HasValue ? 1 : 0;
        int expectedCount = paramTypes.Length - offset;

        if (!funcType.IsFunctionVarArg && nodes.Count != expectedCount)
            new BifyTypeError($"Function expects {expectedCount} arguments, but got {nodes.Count}").Throw();

        var args = new List<LLVMValueRef>(nodes.Count + offset);
        if (thisArg.HasValue)
            args.Add(thisArg.Value);

        for (int i = 0; i < nodes.Count; i++)
        {
            var val = new ExpressionDispatcher(ctx).EmitExpression(nodes[i]);
            int paramIndex = i + offset;
            if (paramIndex < paramTypes.Length && val.Value.TypeOf != paramTypes[paramIndex])
                val = TypeCaster.ImplicitCast(ctx, val, ToBifyType(paramTypes[paramIndex]));
            args.Add(val.Value);
        }
        return args;
    }

    public static BifyType ToBifyType(LLVMTypeRef type)
    {
        var name = type.Kind switch
        {
            LLVMTypeKind.LLVMVoidTypeKind => "void",
            LLVMTypeKind.LLVMFloatTypeKind => "float",
            LLVMTypeKind.LLVMDoubleTypeKind => "double",
            LLVMTypeKind.LLVMIntegerTypeKind => type.IntWidth switch
            {
                1 => "bool",
                8 => "char",
                _ => "int"
            },
            LLVMTypeKind.LLVMPointerTypeKind => "ptr",
            _ => "unknown"
        };
        return new BifyType(name, type);
    }

    private static void FlattenComma(AstNode? node, List<AstNode> list)
    {
        if (node == null) return;
        if (node is AstBinaryOp bin && bin.Token.Type == TokenType.COMMA)
        {
            FlattenComma(bin.Left, list);
            FlattenComma(bin.Right, list);
        }
        else
        {
            list.Add(node);
        }
    }
}