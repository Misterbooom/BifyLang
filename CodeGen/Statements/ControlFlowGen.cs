#nullable enable
using System;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Core.Emitters;
using BifyLang.CodeGen.Expressions;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Statements;

public class ControlFlowGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;
    private LLVMBuilderRef Builder => _ctx.Builder;
    private LLVMValueRef CurrentFunction => _ctx.FunctionEntry.Parent;

    private void EmitBlock(AstNode blockNode) =>
        new StatementDispatcher(_ctx).EmitBlock((AstBlock)blockNode);

    private void EmitStatement(AstNode stmtNode) =>
        new StatementDispatcher(_ctx).EmitStatement(stmtNode);

    private TypedValue EmitExpr(AstNode exprNode) =>
        new ExpressionDispatcher(_ctx).EmitExpression(exprNode);

    private void EmitCondBr(AstNode condition, LLVMBasicBlockRef trueBlock, LLVMBasicBlockRef falseBlock)
    {
        var condBool = BranchEmitter.EmitBoolCondition(_ctx, condition);
        Builder.BuildCondBr(condBool, trueBlock, falseBlock);
    }

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

    public void EmitWhileLoop(AstWhile astWhile)
    {
        var condBlock = CurrentFunction.AppendBasicBlock("while.cond");
        var bodyBlock = CurrentFunction.AppendBasicBlock("while.body");
        var endBlock = CurrentFunction.AppendBasicBlock("while.end");

        Builder.BuildBr(condBlock);
        Builder.PositionAtEnd(condBlock);
        EmitCondBr(astWhile.ConditionNode, bodyBlock, endBlock);

        EmitLoopBody(bodyBlock, endBlock, condBlock, condBlock, astWhile.BlockNode);
        Builder.PositionAtEnd(endBlock);
    }

    public void EmitForLoop(AstFor astFor)
    {
        using (_ctx.EnterScope())
        {
            EmitStatement(astFor.InitNode);

            var condBlock = CurrentFunction.AppendBasicBlock("for.cond");
            var bodyBlock = CurrentFunction.AppendBasicBlock("for.body");
            var incBlock = CurrentFunction.AppendBasicBlock("for.inc");
            var endBlock = CurrentFunction.AppendBasicBlock("for.end");

            Builder.BuildBr(condBlock);
            Builder.PositionAtEnd(condBlock);
            EmitCondBr(astFor.ConditionNode, bodyBlock, endBlock);

            EmitLoopBody(bodyBlock, endBlock, incBlock, incBlock, astFor.BlockNode);

            Builder.PositionAtEnd(incBlock);
            EmitStatement(astFor.IncrementNode);
            Builder.BuildBr(condBlock);

            Builder.PositionAtEnd(endBlock);
        }
    }

    public void EmitSwitch(AstSwitch astSwitch)
    {
        var defaultBlock = CurrentFunction.AppendBasicBlock("switch.default");
        var endBlock = CurrentFunction.AppendBasicBlock("switch.end");
        var exprVal = EmitExpr(astSwitch.ExpressionNode);

        if (exprVal.Type.Type.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
        {
            EmitIntegerSwitch(astSwitch, exprVal, defaultBlock, endBlock);
        }
        else
        {
            EmitFallbackSwitch(astSwitch, astSwitch.ExpressionNode, defaultBlock, endBlock);
        }

        Builder.PositionAtEnd(defaultBlock);
        if (astSwitch.DefaultNode is not null)
            EmitLoopBody(defaultBlock, endBlock, _ctx.LoopContext.ContinueBlock, endBlock, astSwitch.DefaultNode.BlockNode, reposition: false);
        else
            Builder.BranchIfUnterminated(endBlock);

        Builder.FinalizeMergeBlock(endBlock, defaultBlock);
    }

    public void EmitBreak(AstBreak _) =>
        EmitJump(_ctx.LoopContext.BreakBlock, "Break statement must be inside a loop or switch.");

    public void EmitContinue(AstContinue _) =>
        EmitJump(_ctx.LoopContext.ContinueBlock, "Continue statement must be inside a loop.");


    private void EmitLoopBody(
        LLVMBasicBlockRef bodyBlock,
        LLVMBasicBlockRef breakTarget,
        LLVMBasicBlockRef continueTarget,
        LLVMBasicBlockRef fallthroughTarget,
        AstNode blockNode,
        bool reposition = true)
    {
        if (reposition)
            Builder.PositionAtEnd(bodyBlock);

        using (_ctx.EnterLoop(breakTarget, continueTarget))
        using (_ctx.EnterScope())
        {
            EmitBlock(blockNode);
        }

        Builder.BranchIfUnterminated(fallthroughTarget);
    }

    private void EmitIntegerSwitch(AstSwitch astSwitch, TypedValue exprVal, LLVMBasicBlockRef defaultBlock, LLVMBasicBlockRef endBlock)
    {
        var switchInst = Builder.BuildSwitch(exprVal.Value, defaultBlock, (uint)astSwitch.Cases.Count);

        for (int i = 0; i < astSwitch.Cases.Count; i++)
        {
            var caseBlock = CurrentFunction.AppendBasicBlock($"case.{i}");
            var caseVal = CoerceSwitchConstant(EmitExpr(astSwitch.Cases[i].ValueNode).Value, exprVal.Type.Type);

            switchInst.AddCase(caseVal, caseBlock);
            EmitLoopBody(caseBlock, endBlock, _ctx.LoopContext.ContinueBlock, endBlock, astSwitch.Cases[i].BlockNode);
        }
    }

    private void EmitFallbackSwitch(AstSwitch astSwitch, AstNode exprNode, LLVMBasicBlockRef defaultBlock, LLVMBasicBlockRef endBlock)
    {
        if (astSwitch.Cases.Count == 0)
        {
            Builder.BuildBr(defaultBlock);
            return;
        }

        for (int i = 0; i < astSwitch.Cases.Count; i++)
        {
            var caseBlock = CurrentFunction.AppendBasicBlock($"case.{i}");
            var nextBlock = i + 1 < astSwitch.Cases.Count
                ? CurrentFunction.AppendBasicBlock($"switch.next.{i}")
                : defaultBlock;

            var cmp = BinaryOpGen.EmitBinaryOp(_ctx, new ExpressionDispatcher(_ctx), new AstBinaryOp(new Token(TokenType.EQEQ, "=="), astSwitch.Cases[i].ValueNode, exprNode));
            var condBool = TypeCaster.ImplicitCast(_ctx, cmp, BifyType.CreateBool(_ctx));

            Builder.BuildCondBr(condBool.Value, caseBlock, nextBlock);
            EmitLoopBody(caseBlock, endBlock, _ctx.LoopContext.ContinueBlock, endBlock, astSwitch.Cases[i].BlockNode);
            Builder.PositionAtEnd(nextBlock);
        }
    }

    private static LLVMValueRef CoerceSwitchConstant(LLVMValueRef caseVal, LLVMTypeRef expectedType)
    {
        if (caseVal.TypeOf == expectedType)
            return caseVal;

        if (caseVal.IsAConstantInt.Handle == IntPtr.Zero)
            new BifyTypeError("Case value must be a constant integer matching switch type").Throw();

        return LLVMValueRef.CreateConstInt(expectedType, (ulong)caseVal.ConstIntSExt, false);
    }

    private void EmitJump(LLVMBasicBlockRef targetBlock, string errorMessage)
    {
        if (targetBlock.Handle == IntPtr.Zero)
        {
            new BifySyntaxError(errorMessage).Throw();
            return;
        }

        Builder.BranchIfUnterminated(targetBlock);
    }
}