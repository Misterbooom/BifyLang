#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Emitters.FlowControl;

namespace BifyLang.CodeGen.Statements.ControlFlow;

public class WhileGen(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public void EmitWhile(AstWhile astWhile)
    {
        var currentFunction = _ctx.FunctionEntry.Parent;
        var condBlock = currentFunction.AppendBasicBlock("while.cond");
        var bodyBlock = currentFunction.AppendBasicBlock("while.body");
        var endBlock = currentFunction.AppendBasicBlock("while.end");

        _ctx.Builder.BuildBr(condBlock);
        _ctx.Builder.PositionAtEnd(condBlock);
      
            LoopEmitter.EmitCondBr(_ctx, astWhile.ConditionNode, bodyBlock, endBlock);

        LoopEmitter.EmitLoopBody(_ctx, bodyBlock, endBlock, condBlock, condBlock, astWhile.BlockNode);
        _ctx.Builder.PositionAtEnd(endBlock);
    }
}
