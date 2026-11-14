using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Declarations;

public class DeclarationDispatcher(CodeGenContext ctx) : CodeGenBase(ctx)
{

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
            case AstClass astClass:
                new ClassGen(_ctx).EmitClass(astClass);
                break;
            case AstImport astImport:
                new ImportGen(_ctx).EmitImport(astImport);
                break;
            default:
                new BifySyntaxError("Only function and class declarations are allowed at the top level").Throw();
                break;
        }
    }
}