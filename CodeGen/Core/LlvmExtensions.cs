using System;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public static class LlvmExtensions
{
    public static LLVMBasicBlockRef GetBasicBlock(this LLVMValueRef function, string name)
    {
        for (var bb = function.FirstBasicBlock; bb.Handle != IntPtr.Zero; bb = bb.Next)
        {
            if (bb.AsValue().Name == name)
            {
                return bb;
            }
        }

        return default;
    }
    public static bool IsTerminated(this LLVMBasicBlockRef block) =>
        block.Terminator.Handle != IntPtr.Zero;

    public static unsafe bool HasPredecessors(this LLVMBasicBlockRef block) =>
        LLVM.GetFirstUse(LLVM.BasicBlockAsValue(block)) != null;

    public static void BranchIfUnterminated(this LLVMBuilderRef builder, LLVMBasicBlockRef target)
    {
        if (!builder.InsertBlock.IsTerminated())
            builder.BuildBr(target);
    }

    public static void FinalizeMergeBlock(this LLVMBuilderRef builder, LLVMBasicBlockRef mergeBlock, LLVMBasicBlockRef fallbackBlock)
    {
        if (mergeBlock.HasPredecessors())
        {
            var lastBlock = fallbackBlock.Parent.LastBasicBlock;
            if (lastBlock.Handle != mergeBlock.Handle)
                mergeBlock.MoveAfter(lastBlock);

            builder.PositionAtEnd(mergeBlock);
        }
        else
        {
            builder.PositionAtEnd(fallbackBlock);
            mergeBlock.Delete();
        }
    }
}