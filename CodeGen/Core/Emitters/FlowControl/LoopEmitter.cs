#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Statements;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Emitters;

public static class LoopEmitter
{
    public static void EmitCondBr(CodeGenContext ctx, AstNode condition, LLVMBasicBlockRef trueBlock, LLVMBasicBlockRef falseBlock)
    {
        var condBool = BranchEmitter.EmitBoolCondition(ctx, condition);
        ctx.Builder.BuildCondBr(condBool, trueBlock, falseBlock);
    }

    public static void EmitLoopBody(
        CodeGenContext ctx,
        LLVMBasicBlockRef bodyBlock,
        LLVMBasicBlockRef breakTarget,
        LLVMBasicBlockRef continueTarget,
        LLVMBasicBlockRef fallthroughTarget,
        AstNode blockNode,
        bool reposition = true)
    {
        if (reposition)
            ctx.Builder.PositionAtEnd(bodyBlock);

        using (ctx.EnterLoop(breakTarget, continueTarget))
        using (ctx.EnterScope())
            
        {
            new StatementDispatcher(ctx).EmitBlock((AstBlock)blockNode);
        }

        ctx.Builder.BranchIfUnterminated(fallthroughTarget);
    }
}
