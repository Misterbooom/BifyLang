#nullable enable
using System;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Expressions;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Statements;

public class ControlFlowGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public void EmitIfStatement(AstIf astIf)
    {
        var func = _ctx.FunctionEntry.Parent;
        var mergeBlock = func.AppendBasicBlock("if.merge");

        void EmitBranchBody(AstNode body)
        {
            using (_ctx.EnterScope())
                new StatementDispatcher(_ctx).EmitBlock((AstBlock)body);
            if (!IsBlockTerminated(_ctx.Builder.InsertBlock))
                _ctx.Builder.BuildBr(mergeBlock);
        }

        void EmitChain(AstNode cond, AstNode body, int elseIfIdx)
        {
            var thenBlock = func.AppendBasicBlock("if.then");
            var hasElseIf = elseIfIdx < astIf.ElseIfNodes.Count;
            var hasElse = astIf.ElseNode != null;
            var nextBlock = hasElseIf ? func.AppendBasicBlock($"if.cond.{elseIfIdx}") :
                            hasElse ? func.AppendBasicBlock("if.else") : mergeBlock;

            EmitBranch(cond, thenBlock, nextBlock);
            _ctx.Builder.PositionAtEnd(thenBlock);
            EmitBranchBody(body);

            if (hasElseIf)
            {
                _ctx.Builder.PositionAtEnd(nextBlock);
                var elseIf = astIf.ElseIfNodes[elseIfIdx];
                EmitChain(elseIf.ConditionNode, elseIf.BlockNode, elseIfIdx + 1);
            }
            else if (hasElse)
            {
                _ctx.Builder.PositionAtEnd(nextBlock);
                EmitBranchBody(astIf.ElseNode!.BlockNode);
            }
        }

        EmitChain(astIf.ConditionNode, astIf.BlockNode, 0);

        if (HasPredecessor(mergeBlock))
            _ctx.Builder.PositionAtEnd(mergeBlock);
        else
            mergeBlock.Delete();
    }

    public void EmitWhileLoop(AstWhile astWhile)
    {
        var func = _ctx.FunctionEntry.Parent;
        var condBlock = func.AppendBasicBlock("while.cond");
        var bodyBlock = func.AppendBasicBlock("while.body");
        var endBlock = func.AppendBasicBlock("while.end");

        _ctx.Builder.BuildBr(condBlock);
        _ctx.Builder.PositionAtEnd(condBlock);
        EmitBranch(astWhile.ConditionNode, bodyBlock, endBlock);

        _ctx.Builder.PositionAtEnd(bodyBlock);
        using (_ctx.EnterLoop(endBlock, condBlock))
        using (_ctx.EnterScope())
            new StatementDispatcher(_ctx).EmitBlock((AstBlock)astWhile.BlockNode);

        if (!IsBlockTerminated(_ctx.Builder.InsertBlock))
            _ctx.Builder.BuildBr(condBlock);

        _ctx.Builder.PositionAtEnd(endBlock);
    }

    public void EmitForLoop(AstFor astFor)
    {
        using (_ctx.EnterScope())
        {
            new StatementDispatcher(_ctx).EmitStatement(astFor.InitNode);
            var func = _ctx.FunctionEntry.Parent;
            var condBlock = func.AppendBasicBlock("for.cond");
            var bodyBlock = func.AppendBasicBlock("for.body");
            var incrementBlock = func.AppendBasicBlock("for.increment");
            var endBlock = func.AppendBasicBlock("for.end");

            _ctx.Builder.BuildBr(condBlock);
            _ctx.Builder.PositionAtEnd(condBlock);
            EmitBranch(astFor.ConditionNode, bodyBlock, endBlock);

            _ctx.Builder.PositionAtEnd(bodyBlock);
            using (_ctx.EnterLoop(endBlock, incrementBlock))
            using (_ctx.EnterScope())
            {
                new StatementDispatcher(_ctx).EmitBlock((AstBlock)astFor.BlockNode);
            }
            if (!IsBlockTerminated(_ctx.Builder.InsertBlock))
                _ctx.Builder.BuildBr(incrementBlock);

            _ctx.Builder.PositionAtEnd(incrementBlock);
            new StatementDispatcher(_ctx).EmitStatement(astFor.IncrementNode);
            _ctx.Builder.BuildBr(condBlock);

            _ctx.Builder.PositionAtEnd(endBlock);
        }
    }

    public void EmitSwitch(AstSwitch astSwitch)
    {
        var function = _ctx.FunctionEntry.Parent;
        var defaultBlock = function.AppendBasicBlock("switch.default");
        var endBlock = function.AppendBasicBlock("switch.end");

        void EmitCaseBody(AstNode blockNode)
        {
            using (_ctx.EnterLoop(endBlock, _ctx.LoopContext.ContinueBlock))
            using (_ctx.EnterScope())
                new StatementDispatcher(_ctx).EmitBlock((AstBlock)blockNode);

            if (!IsBlockTerminated(_ctx.Builder.InsertBlock))
                _ctx.Builder.BuildBr(endBlock);
        }

        var expressionValue = new ExpressionDispatcher(_ctx).EmitExpression(astSwitch.ExpressionNode);

        if (expressionValue.Type.Type.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
        {
            var switchInst = _ctx.Builder.BuildSwitch(
                expressionValue.Value,
                defaultBlock,
                Convert.ToUInt32(astSwitch.Cases.Count)
            );

            for (int i = 0; i < astSwitch.Cases.Count; i++)
            {
                var caseBlock = function.AppendBasicBlock($"case.{i}");
                var caseValue = new ExpressionDispatcher(_ctx).EmitExpression(astSwitch.Cases[i].ValueNode);
                var caseVal = caseValue.Value;
                if (caseVal.TypeOf != expressionValue.Type.Type)
                {
                    if (caseVal.IsAConstantInt.Handle != IntPtr.Zero)
                        caseVal = LLVMValueRef.CreateConstInt(expressionValue.Type.Type, (ulong)caseVal.ConstIntSExt, false);
                    else
                        new BifyTypeError("Case value must be a constant integer matching switch type").Throw();
                }

                switchInst.AddCase(caseVal, caseBlock);
                _ctx.Builder.PositionAtEnd(caseBlock);
                EmitCaseBody(astSwitch.Cases[i].BlockNode);
            }
        }
        else
        {
            for (int i = 0; i < astSwitch.Cases.Count; i++)
            {
                var caseBlock = function.AppendBasicBlock($"case.{i}");
                var nextBlock = i + 1 < astSwitch.Cases.Count
                    ? function.AppendBasicBlock($"switch.next.{i}")
                    : defaultBlock;

                var caseValue = new ExpressionDispatcher(_ctx).EmitExpression(astSwitch.Cases[i].ValueNode);
                var cmp = BinaryOpGen.EmitBinary(_ctx, TokenType.EQEQ, expressionValue, caseValue);
                var condBool = TypeCaster.ImplicitCast(_ctx, cmp, BifyType.CreateBool(_ctx));
                _ctx.Builder.BuildCondBr(condBool.Value, caseBlock, nextBlock);

                _ctx.Builder.PositionAtEnd(caseBlock);
                EmitCaseBody(astSwitch.Cases[i].BlockNode);

                _ctx.Builder.PositionAtEnd(nextBlock);
            }

            if (astSwitch.Cases.Count == 0)
                _ctx.Builder.BuildBr(defaultBlock);
        }

        _ctx.Builder.PositionAtEnd(defaultBlock);
        if (astSwitch.DefaultNode != null)
            EmitCaseBody(astSwitch.DefaultNode.BlockNode);
        else if (!IsBlockTerminated(_ctx.Builder.InsertBlock))
            _ctx.Builder.BuildBr(endBlock);

        if (HasPredecessor(endBlock))
            _ctx.Builder.PositionAtEnd(endBlock);
        else
            endBlock.Delete();
    }

    private static bool IsBlockTerminated(LLVMBasicBlockRef bb) => bb.Terminator.Handle != IntPtr.Zero;

    private static unsafe bool HasPredecessor(LLVMBasicBlockRef block) =>
        LLVM.GetFirstUse(LLVM.BasicBlockAsValue(block)) != null;

    public void EmitBreak(AstBreak astBreak)
    {
        if (_ctx.LoopContext.BreakBlock.Handle == IntPtr.Zero)
        {
            new BifySyntaxError("Break statement must be inside a loop or switch.").Throw();
            return;
        }

        if (IsBlockTerminated(_ctx.Builder.InsertBlock)) return;
        _ctx.Builder.BuildBr(_ctx.LoopContext.BreakBlock);
        var deadBlock = _ctx.FunctionEntry.Parent.AppendBasicBlock("dead");
        _ctx.Builder.PositionAtEnd(deadBlock);
    }

    public void EmitContinue(AstContinue astContinue)
    {
        if (_ctx.LoopContext.ContinueBlock.Handle == IntPtr.Zero)
        {
            new BifySyntaxError("Continue statement must be inside a loop.").Throw();
            return;
        }

        if (IsBlockTerminated(_ctx.Builder.InsertBlock)) return;
        _ctx.Builder.BuildBr(_ctx.LoopContext.ContinueBlock);
        var deadBlock = _ctx.FunctionEntry.Parent.AppendBasicBlock("dead");
        _ctx.Builder.PositionAtEnd(deadBlock);
    }

    private void EmitBranch(AstNode condition, LLVMBasicBlockRef trueBlock, LLVMBasicBlockRef falseBlock)
    {
        var val = new ExpressionDispatcher(_ctx).EmitExpression(condition);
        var boolVal = TypeCaster.ImplicitCast(_ctx, val, BifyType.CreateBool(_ctx));
        _ctx.Builder.BuildCondBr(boolVal.Value, trueBlock, falseBlock);
    }
}