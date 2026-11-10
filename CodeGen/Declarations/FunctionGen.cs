using System;
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Statements;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Declarations;

public class FunctionGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    private readonly List<ParameterInfo> _parameters = [];

   

    public void EmitFunction(AstFunctionDecl functionDecl)
    {
        string functionName = functionDecl.FunctionNameNode.Name;

        BifyType returnType = TypeResolver.Resolve(
            functionDecl.TypeNode,
            _ctx
        );

       

        var pathChecker = new FunctionPathChecker(
            functionDecl,
            _ctx
        );

        if (!pathChecker.AllPathsReturn &&
            returnType.Type.Kind != LLVMTypeKind.LLVMVoidTypeKind)
        {
            new BifyTypeError(
                $"Not all execution paths return a value in function {functionName}"
            ).Throw();
        }

        _parameters.Clear();
        _parameters.AddRange(ParamResolver.Resolve(functionDecl.ArgumentsNode, _ctx));

        var functionType = LLVMTypeRef.CreateFunction(
            returnType.Type,
            _parameters
                .Select(parameter => parameter.Type.Type)
                .ToArray(),
            false
        );

        var functionValue = _ctx.Module.AddFunction(
            functionName,
            functionType
        );

        var entry = functionValue.AppendBasicBlock("entry");
        

        _ctx.Builder.PositionAtEnd(entry);
        _ctx.FunctionEntry = entry;
        _ctx.FunctionReturnType = returnType;
        _ctx.CurrentScope.Define(
            functionName,
            functionValue,
            new BifyType(returnType.Name, functionType)
        );
        using (_ctx.EnterScope())
        {
            AddFunctionParametersToScope(functionValue);
            StatementDispatcher statementDispatcher = new StatementDispatcher(_ctx);
            statementDispatcher.EmitBlock(functionDecl.BlockNode);
        }
        
        if (returnType.Type.Kind == LLVMTypeKind.LLVMVoidTypeKind && _ctx.Builder.InsertBlock.Terminator.Handle == IntPtr.Zero)
        {
            _ctx.Builder.BuildRetVoid();
        }
    }


    private void AddFunctionParametersToScope(
        LLVMValueRef functionValue
    )
    {
        for (uint i = 0; i < _parameters.Count; i++)
        {
            var parameter = _parameters[(int)i];

            var alloca = _ctx.Builder.BuildAlloca(
                parameter.Type.Type,
                parameter.Name
            );

            var paramVal = functionValue.GetParam(i);
            _ctx.Builder.BuildStore(paramVal, alloca);

            if (parameter.Type is ClassType)
            {
                _ctx.HeapControl.BuildRetainStrong(paramVal);
            }

            _ctx.CurrentScope.Define(
                parameter.Name,
                alloca,
                parameter.Type,
                parameter.IsConst
            );
        }
    }
}