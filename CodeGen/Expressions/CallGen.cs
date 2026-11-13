#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
        var target = new ExpressionDispatcher(_ctx).EmitExpression(astCall.CallableName);
        if (!target.IsCallable)
        {
            return new BifyTypeError("Can't call  " + astCall.CallableName.Token.Value).Throw<TypedValue>();
        }

        CallableType? callableType = target.Type as CallableType;
        if (callableType == null)
        {
            throw new Exception($"Type {target.Type} is not a callable type");
        }
        CheckForMethod(out var thisArg, callableType);

        
        var args = ResolveArgs(_ctx, astCall, callableType, thisArg);
        BifyDebug.Log(target.ToString());
        
        var callReturn = _ctx.Builder.BuildCall2(callableType.Type, target.Value, args.Select(i => i.Value).ToArray());
        return new TypedValue(callReturn, callableType.ReturnType);
    }


    public static TypedValue[] ResolveArgs(
        CodeGenContext ctx,
        AstCall astCall,
        CallableType callableType,
        TypedValue? thisArg)
    {
        List<AstNode> flatArgs = [];
        ParamResolver.FlattenComma(astCall.ArgumentsNode, flatArgs);

        int offset = thisArg.HasValue ? 1 : 0;
        ValidateArgCount(callableType, flatArgs.Count, offset);

        var args = new TypedValue[flatArgs.Count + offset];
        if (thisArg.HasValue)
        {
            args[0] = thisArg.Value;
        }

        var dispatcher = new ExpressionDispatcher(ctx);
        for (int i = 0; i < flatArgs.Count; i++)
        {
            args[i + offset] = ResolveSingleArg(ctx, dispatcher, flatArgs[i], callableType, i + offset);
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

    void CheckForMethod(out TypedValue? thisArg, CallableType callableType)
    {
        thisArg = null;

        if (!callableType.IsMethod)
        {
            return;
        }

        if (_ctx.CurrentScope.TryLookup("this", out var thisSym))
        {
            thisArg = new TypedValue(thisSym.Pointer,thisSym.Type, isCallable:true);
        }
    }

}