#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Core.Emitters;

namespace BifyLang.CodeGen.Statements;

public class IfGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    private void EmitBlock(AstNode blockNode) =>
        new StatementDispatcher(_ctx).EmitBlock((AstBlock)blockNode);

    public void EmitIfStatement(AstIf astIf)
    {
        using var branchEmitter = new BranchEmitter(_ctx, astIf.ElseIfNodes.Count, astIf.ElseNode is not null);
        
        using (branchEmitter.EnterCondBranch(astIf.ConditionNode))
        {
            EmitBlock(astIf.BlockNode);
        }

        foreach (var block in branchEmitter.EmitChain(astIf.ElseIfNodes))
        {
            EmitBlock(block);
        }

        if (astIf.ElseNode is not null)
        {
            using (branchEmitter.EnterBranch())
            {
                EmitBlock(astIf.ElseNode.BlockNode);
            }
        }
    }
}