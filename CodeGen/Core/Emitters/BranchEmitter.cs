#nullable enable
using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen.Expressions;
using BifyLang.CodeGen.Types;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Emitters;


public readonly struct BranchScope : IDisposable
{
    private readonly LLVMBuilderRef _builder;
    private readonly LLVMBasicBlockRef _mergeBlock;
    private readonly LLVMBasicBlockRef _falseTargetBlock;
    private readonly IDisposable _lexicalScope;

    public BranchScope(
        CodeGenContext ctx,
        LLVMBasicBlockRef bodyBlock,
        LLVMBasicBlockRef falseTargetBlock,
        LLVMBasicBlockRef mergeBlock,
        LLVMValueRef? condition)
    {
        _builder = ctx.Builder;
        _mergeBlock = mergeBlock;
        _falseTargetBlock = falseTargetBlock;

        if (condition.HasValue)
            _builder.BuildCondBr(condition.Value, bodyBlock, falseTargetBlock);

        _builder.PositionAtEnd(bodyBlock);
        _lexicalScope = ctx.EnterScope();
    }

    public void Dispose()
    {
        _lexicalScope.Dispose();
        _builder.BranchIfUnterminated(_mergeBlock);
        _builder.PositionAtEnd(_falseTargetBlock);
    }
}

public sealed class BranchEmitter : IDisposable
{
    private readonly CodeGenContext _ctx;
    private readonly LLVMBasicBlockRef[] _condBlocks;
    private readonly LLVMBasicBlockRef? _elseBlock;
    private readonly LLVMBasicBlockRef _mergeBlock;
    private LLVMBasicBlockRef _lastBodyBlock;
    private int _currentBranchIndex;

    public BranchEmitter(CodeGenContext ctx, int elseIfCount, bool hasElse)
    {
        _ctx = ctx;
        var fn = _ctx.FunctionEntry.Parent;
        _lastBodyBlock = _ctx.Builder.InsertBlock;

        _condBlocks = new LLVMBasicBlockRef[elseIfCount];
        for (int i = 0; i < elseIfCount; i++)
            _condBlocks[i] = fn.AppendBasicBlock($"elseif.cond.{i}");

        if (hasElse)
            _elseBlock = fn.AppendBasicBlock("else.body");

        _mergeBlock = fn.AppendBasicBlock("if.merge");
    }

    public BranchScope EnterCondBranch(AstNode conditionNode)
    {
        var fn = _ctx.FunctionEntry.Parent;
        LLVMValueRef condBool = EmitBoolCondition(_ctx, conditionNode);

        string blockName = _currentBranchIndex == 0 ? "if.then" : $"elseif.body.{_currentBranchIndex - 1}";
        LLVMBasicBlockRef bodyBlock = fn.AppendBasicBlock(blockName);
        LLVMBasicBlockRef falseTarget = GetNextFalseTarget();

        _lastBodyBlock = bodyBlock;
        _currentBranchIndex++;

        return new BranchScope(_ctx, bodyBlock, falseTarget, _mergeBlock, condBool);
    }

    public IEnumerable<AstBlock> EmitChain(IReadOnlyList<AstElseIf> branches)
    {
        for (int i = 0; i < branches.Count; i++)
        {
            using (EnterCondBranch(branches[i].ConditionNode))
            {
                yield return (AstBlock)branches[i].BlockNode;
            }
        }
    }

    public BranchScope EnterBranch()
    {
        if (!_elseBlock.HasValue)
            throw new InvalidOperationException("Else block was not declared.");

        _lastBodyBlock = _elseBlock.Value;
        return new BranchScope(_ctx, _elseBlock.Value, _mergeBlock, _mergeBlock, null);
    }

    public static LLVMValueRef EmitBoolCondition(CodeGenContext ctx, AstNode conditionNode)
    {
        var rawVal = new ExpressionDispatcher(ctx).EmitExpression(conditionNode);
        return TypeCaster.ImplicitCast(ctx, rawVal, BifyType.CreateBool(ctx)).Value;
    }

    private LLVMBasicBlockRef GetNextFalseTarget() =>
        _currentBranchIndex < _condBlocks.Length
            ? _condBlocks[_currentBranchIndex]
            : (_elseBlock ?? _mergeBlock);

    public void Dispose() =>
        _ctx.Builder.FinalizeMergeBlock(_mergeBlock, _lastBodyBlock);
}