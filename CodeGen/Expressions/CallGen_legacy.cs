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

public class CallGen_legacy(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public TypedValue EmitCall(AstCall astCall)
    {
        LLVMValueRef callee;
        BifyType funcType;
        string retName;
        LLVMValueRef? thisArg = null;
        if (astCall.CallableName is AstMemberAccess memberAccess)
        {
            
            var target = new ExpressionDispatcher(_ctx).EmitExpression(memberAccess.Left);
            if (target.Type is ModuleType)
            {
                (callee, funcType) = ResolveCallable(astCall.CallableName);
                
            }
            else
            {
                if (target.Type is not ClassType classType)
                    return new BifyTypeError($"Cannot call member on non-class type '{target.Type.Name}'").Throw<TypedValue>();

                if (memberAccess.Right is not AstIdentifier id || !classType.Methods.TryGetValue(id.Name, out var method))
                    return new BifyAttributeError($"Type '{target.Type.Name}' has no method '{memberAccess.Right?.Token.Value}'").Throw<TypedValue>();
            
                MemberResolver.CheckAccess(_ctx, classType, method.AccessLevel, method.Name);
                if (target.IsWeak)
                {
                    var upgraded = _ctx.HeapControl.BuildUpgradeWeak(target.Value);
                    target = new TypedValue(upgraded, target.Type, false);
                    _ctx.HeapControl.TempRefs.Add(target);
                }
                funcType = method.Signature;
                retName = method.Signature.Name;
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
                    string symbol = NameMangler.GenerateMethodName(_ctx.ModuleContext.Name, classType.Name, id.Name);
                    callee = _ctx.Module.GetNamedFunction(symbol);
                    if (callee.Handle == IntPtr.Zero)
                        callee = _ctx.Module.AddFunction(symbol, method.Signature.Type);
                }
            }
            
        }
        else
        {
            (callee, funcType) = ResolveCallable(astCall.CallableName);
        }

        if (callee.Handle == IntPtr.Zero || funcType.Type.Handle == IntPtr.Zero)
            return default;

        var args = ResolveArgs(_ctx, astCall.ArgumentsNode, funcType, thisArg);
        if (!funcType.IsFunctionVarArg && args.Count != funcType.GetParamTypes().Length)
            return default;

        var callReturn = _ctx.Builder.BuildCall2(funcType, callee, [.. args]);
        var resType = _ctx.TypeRegistry.GetBifyType(retName);
        return new TypedValue(callReturn, resType);
    }

    private unsafe (LLVMValueRef Callee, BifyType type) ResolveCallable(AstNode node)
    {
        if (node is AstIdentifier id)
        {
            if (_ctx.CurrentScope.TryLookup(id.Name, out var symbol) && symbol.Type.Type.Kind == LLVMTypeKind.LLVMFunctionTypeKind)
                return (symbol.Pointer, symbol.Type);

            

            new BifyNameError($"Function '{id.Name}' is not defined").Throw();
            return (default, default);
        }
        var callable = new ExpressionDispatcher(_ctx).EmitExpression(node);
        return (callable.Value, callable.Type);
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

    public static void FlattenComma(AstNode? node, List<AstNode> list)
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