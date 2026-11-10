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

public class SwitchGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public void EmitSwitch(AstSwitch astSwitch)
    {
        var currentFunction = _ctx.FunctionEntry.Parent;
        var defaultBlock = currentFunction.AppendBasicBlock("switch.default");
        var endBlock = currentFunction.AppendBasicBlock("switch.end");
        var exprVal = new ExpressionDispatcher(_ctx).EmitExpression(astSwitch.ExpressionNode);

        if (exprVal.Type.Type.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
        {
            EmitIntegerSwitch(astSwitch, exprVal, defaultBlock, endBlock);
        }
        else
        {
            EmitFallbackSwitch(astSwitch, astSwitch.ExpressionNode, defaultBlock, endBlock);
        }

        _ctx.Builder.PositionAtEnd(defaultBlock);
        if (astSwitch.DefaultNode is not null)
            LoopEmitter.EmitLoopBody(_ctx, defaultBlock, endBlock, _ctx.LoopContext.ContinueBlock, endBlock, astSwitch.DefaultNode.BlockNode, reposition: false);
        else
            _ctx.Builder.BranchIfUnterminated(endBlock);

        _ctx.Builder.FinalizeMergeBlock(endBlock, defaultBlock);
    }

    private void EmitIntegerSwitch(AstSwitch astSwitch, TypedValue exprVal, LLVMBasicBlockRef defaultBlock, LLVMBasicBlockRef endBlock)
    {
        var switchInst = _ctx.Builder.BuildSwitch(exprVal.Value, defaultBlock, (uint)astSwitch.Cases.Count);

        for (int i = 0; i < astSwitch.Cases.Count; i++)
        {
            var caseBlock = _ctx.FunctionEntry.Parent.AppendBasicBlock($"case.{i}");
            var caseVal = CoerceSwitchConstant(new ExpressionDispatcher(_ctx).EmitExpression(astSwitch.Cases[i].ValueNode).Value, exprVal.Type.Type);

            switchInst.AddCase(caseVal, caseBlock);
            LoopEmitter.EmitLoopBody(_ctx, caseBlock, endBlock, _ctx.LoopContext.ContinueBlock, endBlock, astSwitch.Cases[i].BlockNode);
        }
    }

    private void EmitFallbackSwitch(AstSwitch astSwitch, AstNode exprNode, LLVMBasicBlockRef defaultBlock, LLVMBasicBlockRef endBlock)
    {
        if (astSwitch.Cases.Count == 0)
        {
            _ctx.Builder.BuildBr(defaultBlock);
            return;
        }

        for (int i = 0; i < astSwitch.Cases.Count; i++)
        {
            var caseBlock = _ctx.FunctionEntry.Parent.AppendBasicBlock($"case.{i}");
            var nextBlock = i + 1 < astSwitch.Cases.Count
                ? _ctx.FunctionEntry.Parent.AppendBasicBlock($"switch.next.{i}")
                : defaultBlock;

            var cmp = BinaryOpGen.EmitBinaryOp(_ctx, new ExpressionDispatcher(_ctx), new AstBinaryOp(new Token(TokenType.EQEQ, "=="), astSwitch.Cases[i].ValueNode, exprNode));
            var condBool = TypeCaster.ImplicitCast(_ctx, cmp, BifyType.CreateBool(_ctx));

            _ctx.Builder.BuildCondBr(condBool.Value, caseBlock, nextBlock);
            LoopEmitter.EmitLoopBody(_ctx, caseBlock, endBlock, _ctx.LoopContext.ContinueBlock, endBlock, astSwitch.Cases[i].BlockNode);
            _ctx.Builder.PositionAtEnd(nextBlock);
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
}
