using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Declarations;

public class DeclarationDispatcher(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public void EmitModule(AstModule module)
    {
        foreach (var node in module.ChildNodes)
        {
            EmitDeclaration(node);
        }
    }

    public void EmitDeclaration(AstNode node)
    {
        Traceback.Instance.Line = node.LineNumber;
        switch (node)
        {
            case AstFunctionDecl functionDecl:
                new FunctionGen(_ctx).EmitFunction(functionDecl);
                break;
            case AstClass:
                // Layout is built in ClassLayoutBuilder; method body emission in next phase
                break;
            default:
                new BifySyntaxError("Only function and class declarations are allowed at the top level").Throw();
                break;
        }
    }
}