#nullable enable
using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class NewGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public TypedValue EmitNew(AstNew astNew)
    {
        string? className = TryExtractClassName(astNew.ValueNode);
        if (className == null)
            return new BifySyntaxError("Expected class identifier after 'new'").Throw<TypedValue>();

        var bifyType = _ctx.TypeRegistry.GetBifyType(className);
        if (bifyType is not ClassType classType)
            return new BifyTypeError($"'{className}' is not a class").Throw<TypedValue>();

        var classBuilder = new ClassBuilder(_ctx);
        var instancePtr = classBuilder.BuildInstance(classType);

        InvokeConstructor(classType, instancePtr, astNew.ArgumentsNode);

        return new TypedValue(instancePtr, classType);
    }

    private void InvokeConstructor(ClassType classType, LLVMValueRef instancePtr, AstNode? argsNode)
    {
        if (classType.Methods.TryGetValue("constructor", out var ctor))
        {
            var symbol = $"{classType.Name}_constructor";
            var func = _ctx.Module.GetNamedFunction(symbol);
            if (func.Handle == IntPtr.Zero)
            {
                new BifyNameError($"Constructor function '{symbol}' is not defined").Throw();
                return;
            }

            var args = ResolveConstructorArgs(instancePtr, ctor, argsNode);
            _ctx.Builder.BuildCall2(ctor.Signature.Type, func, [.. args]);
        }
        else if (argsNode != null)
        {
            var argNodes = new List<AstNode>();
            ParamResolver.FlattenComma(argsNode, argNodes);
            if (argNodes.Count > 0)
            {
                new BifyTypeError($"Class '{classType.Name}' has no constructor matching arguments").Throw();
            }
        }
    }

    private List<LLVMValueRef> ResolveConstructorArgs(
        LLVMValueRef instancePtr,
        ClassMethod ctor,
        AstNode? argsNode)
    {
        var argNodes = new List<AstNode>();
        ParamResolver.FlattenComma(argsNode, argNodes);

        if (argNodes.Count != ctor.Parameters.Count)
        {
            new BifyTypeError(
                $"Constructor '{ctor.DeclaringClass}' expects {ctor.Parameters.Count} arguments, but got {argNodes.Count}"
            ).Throw();
        }

        var args = new List<LLVMValueRef>(argNodes.Count + 1) { instancePtr };
        for (int i = 0; i < argNodes.Count; i++)
        {
            var expectedType = ctor.Parameters[i].Type;
            var val = new ExpressionDispatcher(_ctx).EmitExpression(argNodes[i]);
            if (val.Value.TypeOf != expectedType.Type)
            {
                val = TypeCaster.ImplicitCast(_ctx, val, expectedType);
            }

            args.Add(val.Value);
        }

        return args;
    }

    private string? TryExtractClassName(AstNode node) => node switch
    {
        AstIdentifier id => NameMangler.GenerateClassName(_ctx.ModuleContext.Name, id.Name),
        AstMemberAccess ma when
            TryExtractClassName(ma.Left) is { } left &&
            ma.Right is AstIdentifier right => NameMangler.GenerateClassName(left, right.Name),
        _ => null
    };
}