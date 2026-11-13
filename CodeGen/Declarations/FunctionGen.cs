#nullable enable
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Declarations;

public class FunctionGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;
    private readonly FunctionBodyEmitter _bodyEmitter = new(ctx);

    public LLVMValueRef DeclareFunction(AstFunctionDecl functionDecl)
    {
        string functionName = functionDecl.FunctionNameNode.Name;
        var mangledName = NameMangler.GenerateFunctionName(_ctx.ModuleContext.Name, functionName);

        var existingFn = _ctx.Module.GetNamedFunction(mangledName);
        if (existingFn.Handle != 0)
        {
            return existingFn;
        }

        var returnType = TypeResolver.Resolve(functionDecl.TypeNode, _ctx);
        var parameters = ParamResolver.Resolve(functionDecl.ArgumentsNode, _ctx);
        var paramTypes = parameters.Select(p => p.Type.Type).ToArray();

        
        var functionType = LLVMTypeRef.CreateFunction(returnType.Type, paramTypes, false);
        var functionValue = _ctx.Module.AddFunction(mangledName, functionType);

        _ctx.CurrentScope.Define(functionName, functionValue, new CallableType(functionName, mangledName, [..parameters], returnType, functionType));

        return functionValue;
    }

 
    public void EmitFunctionBody(AstFunctionDecl functionDecl)
    {
        string functionName = functionDecl.FunctionNameNode.Name;
        var mangledName = NameMangler.GenerateFunctionName(_ctx.ModuleContext.Name, functionName);
        
        var returnType = TypeResolver.Resolve(functionDecl.TypeNode, _ctx);

        var pathChecker = new FunctionPathChecker(functionDecl, _ctx);
        if (!pathChecker.AllPathsReturn && returnType.Type.Kind != LLVMTypeKind.LLVMVoidTypeKind)
        {
            new BifyTypeError($"Not all execution paths return a value in function {mangledName}").Throw();
        }

        var parameters = ParamResolver.Resolve(functionDecl.ArgumentsNode, _ctx);

        var functionValue = _ctx.Module.GetNamedFunction(mangledName);
        if (functionValue.Handle == 0)
        {
            functionValue = DeclareFunction(functionDecl);
        }

        _bodyEmitter.Emit(functionValue, returnType, parameters, functionDecl.BlockNode);
    }


    public void EmitFunction(AstFunctionDecl functionDecl)
    {
        DeclareFunction(functionDecl);
        EmitFunctionBody(functionDecl);
    }
}