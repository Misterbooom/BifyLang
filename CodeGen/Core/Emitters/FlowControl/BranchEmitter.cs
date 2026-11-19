#nullable enable
using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Expressions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Emitters.FlowControl;




public sealed class BranchEmitter : CodeGenBase, IDisposable
{
    private readonly LLVMBasicBlockRef[] _condBlocks;
    private readonly LLVMBasicBlockRef? _elseBlock;
    private readonly LLVMBasicBlockRef _mergeBlock;
    private LLVMBasicBlockRef _lastBodyBlock;
    private int _currentBranchIndex;

    public BranchEmitter(CodeGenContext ctx, int elseIfCount, bool hasElse) : base(ctx)
    {
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
        using (ctx.HeapControl.BeginTempScope())
        {
            var rawVal = new ExpressionDispatcher(ctx).EmitExpression(conditionNode);
            return TypeCaster.ImplicitCast(ctx, rawVal, BifyType.CreateBool(ctx)).Value;
        }
    }

    private LLVMBasicBlockRef GetNextFalseTarget() =>
        _currentBranchIndex < _condBlocks.Length
            ? _condBlocks[_currentBranchIndex]
            : (_elseBlock ?? _mergeBlock);

    public void Dispose() =>
        _ctx.Builder.FinalizeMergeBlock(_mergeBlock, _lastBodyBlock);
}