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
        var (callee, funcType, retName) = ResolveCallable(astCall.CallableName);
        if (callee.Handle == IntPtr.Zero || funcType.Handle == IntPtr.Zero)
            return default;

        var args = ResolveArgs(_ctx, astCall.ArgumentsNode, funcType);
        if (!funcType.IsFunctionVarArg && args.Count != funcType.GetParamTypes().Length)
            return default;

        var callReturn = _ctx.Builder.BuildCall2(funcType, callee, [.. args]);
        var resType = _ctx.TypeRegistry.GetBifyType(retName);
        return new TypedValue(callReturn, resType, false, resType is ClassType);
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
            return (default, default, "unknown");
        }

        var callable = new ExpressionDispatcher(_ctx).EmitExpression(node);
        return (callable.Value, callable.Type.Type, callable.Type.Name);
    }

    public static List<LLVMValueRef> ResolveArgs(CodeGenContext ctx, AstNode? node, LLVMTypeRef funcType)
    {
        var nodes = new List<AstNode>();
        FlattenComma(node, nodes);

        var paramTypes = funcType.GetParamTypes();
        int expectedCount = paramTypes.Length;

        if (!funcType.IsFunctionVarArg && nodes.Count != expectedCount)
            new BifyTypeError($"Function expects {expectedCount} arguments, but got {nodes.Count}").Throw();

        var args = new List<LLVMValueRef>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            var val = new ExpressionDispatcher(ctx).EmitExpression(nodes[i]);
            if (i < paramTypes.Length && val.Value.TypeOf != paramTypes[i])
                val = TypeCaster.ImplicitCast(ctx, val, ToBifyType(paramTypes[i]));
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