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

    public void EmitFunction(AstFunctionDecl functionDecl)
    {
        string functionName = functionDecl.FunctionNameNode.Name;
        var returnType = TypeResolver.Resolve(functionDecl.TypeNode, _ctx);

        var pathChecker = new FunctionPathChecker(functionDecl, _ctx);
        if (!pathChecker.AllPathsReturn && returnType.Type.Kind != LLVMTypeKind.LLVMVoidTypeKind)
        {
            new BifyTypeError($"Not all execution paths return a value in function {functionName}").Throw();
        }

        var parameters = ParamResolver.Resolve(functionDecl.ArgumentsNode, _ctx);
        var paramTypes = parameters.Select(p => p.Type.Type).ToArray();
        var functionType = LLVMTypeRef.CreateFunction(returnType.Type, paramTypes, false);
        var functionValue = _ctx.Module.AddFunction(functionName, functionType);

        _ctx.CurrentScope.Define(functionName, functionValue, new BifyType(returnType.Name, functionType));
        _bodyEmitter.Emit(functionValue, returnType, parameters, functionDecl.BlockNode);
    }
}