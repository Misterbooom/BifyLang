using System;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public class LoopContext(LoopContext parent, LLVMBasicBlockRef breakBlock, LLVMBasicBlockRef continueBlock)
{
    public LoopContext? Parent { get; } = parent;

    public readonly LLVMBasicBlockRef BreakBlock = breakBlock;
    public readonly LLVMBasicBlockRef ContinueBlock = continueBlock;
}