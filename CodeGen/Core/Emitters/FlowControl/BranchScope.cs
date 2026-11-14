using System;
using BifyLang.CodeGen.Core.Context;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Emitters.FlowControl;

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