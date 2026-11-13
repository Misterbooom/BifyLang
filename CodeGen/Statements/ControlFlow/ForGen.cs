#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Core.Emitters;

namespace BifyLang.CodeGen.Statements;

public class ForGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public void EmitForLoop(AstFor astFor)
    {
        using (_ctx.EnterScope())
        {
            new StatementDispatcher(_ctx).EmitStatement(astFor.InitNode);

            var currentFunction = _ctx.FunctionEntry.Parent;
            var condBlock = currentFunction.AppendBasicBlock("for.cond");
            var bodyBlock = currentFunction.AppendBasicBlock("for.body");
            var incBlock = currentFunction.AppendBasicBlock("for.inc");
            var endBlock = currentFunction.AppendBasicBlock("for.end");

            _ctx.Builder.BranchIfUnterminated(condBlock);
            _ctx.Builder.PositionAtEnd(condBlock);
            
                LoopEmitter.EmitCondBr(_ctx, astFor.ConditionNode, bodyBlock, endBlock);
             
            
            

            LoopEmitter.EmitLoopBody(_ctx, bodyBlock, endBlock, incBlock, incBlock, astFor.BlockNode);

            _ctx.Builder.PositionAtEnd(incBlock);
            using (_ctx.HeapControl.BeginTempScope())
                new StatementDispatcher(_ctx).EmitStatement(astFor.IncrementNode);
            
            _ctx.Builder.BranchIfUnterminated(condBlock);

            _ctx.Builder.PositionAtEnd(endBlock);
        }
    }
}
