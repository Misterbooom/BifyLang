#nullable enable
using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Emitters;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class NewGen(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public TypedValue EmitNew(AstNew astNew)
    {
        var classType = ResolveClassType(astNew.ValueNode);
        var instancePtr = new ClassBuilder(_ctx).BuildInstance(classType);

        InvokeConstructor(classType, instancePtr, astNew.ArgumentsNode);
        return new TypedValue(instancePtr, classType);
    }

    private ClassType ResolveClassType(AstNode valueNode)
    {
        string? rawName = valueNode switch
        {
            AstIdentifier id => NameMangler.GenerateClassName(_ctx.ModuleContext.Name, id.Name),
            AstMemberAccess { Left: AstIdentifier mod, Right: AstIdentifier cls } => NameMangler.GenerateClassName(mod.Name, cls.Name),
            _ => null
        };

        if (rawName == null)
            return Fail<ClassType>("Expected class identifier after 'new'", DiagnosticKind.Syntax, valueNode);

        if (TypeResolver.Resolve(valueNode, _ctx) is not ClassType classType)
            return Fail<ClassType>($"'{rawName}' is not a class", DiagnosticKind.Type, valueNode);

        return classType;
    }

    private void InvokeConstructor(ClassType classType, LLVMValueRef instancePtr, AstNode? argsNode)
    {
        List<AstNode> argNodes = [];
        ParameterResolver.FlattenComma(argsNode, argNodes);

        if (!classType.Methods.TryGetValue("constructor", out var ctor))
        {
            // if (argNodes.Count > 0)
            //     Fail($"Class '{classType.Name}' has no constructor taking {argNodes.Count} argument(s)", DiagnosticKind.Type, argsNode);
            throw new InvalidOperationException("Cannot find constructor in class. Probably constructor synthezation failed.");
        }

        if (ctor.IsPoison)
            AbortPoison();

        if (argNodes.Count != ctor.Parameters.Count)
            Fail($"Constructor '{classType.Name}' expects {ctor.Parameters.Count} argument(s), got {argNodes.Count}", DiagnosticKind.Type, argsNode);

        var func = ResolveConstructorFn(ctor);
        var args = EmitConstructorArgs(instancePtr, ctor.Parameters, argNodes);
        _ctx.Builder.BuildCall2(ctor.Signature.Type, func, args);
    }

    private LLVMValueRef ResolveConstructorFn(ClassMethod ctor)
    {
        if (ctor.FunctionValue.Handle != IntPtr.Zero)
            return ctor.FunctionValue;

        string symbol = ctor.Signature is CallableType c ? c.FullName : $"{ctor.DeclaringClass.FullName}_constructor";
        var func = _ctx.Module.GetNamedFunction(symbol);
        if (func.Handle == IntPtr.Zero)
            Fail($"Constructor '{symbol}' is not defined", DiagnosticKind.Name);

        return func;
    }

    private LLVMValueRef[] EmitConstructorArgs(LLVMValueRef instancePtr, List<ParameterInfo> parameters, List<AstNode> argNodes)
    {
        var dispatcher = new ExpressionDispatcher(_ctx);
        var args = new LLVMValueRef[argNodes.Count + 1];
        args[0] = instancePtr;

        for (int i = 0; i < argNodes.Count; i++)
        {
            var val = dispatcher.EmitExpression(argNodes[i]);
            args[i + 1] = TypeCaster.ImplicitCast(_ctx, val, parameters[i].Type).Value;
        }

        return args;
    }
}
