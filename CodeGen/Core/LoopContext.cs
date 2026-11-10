using System;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public class LoopContext(LoopContext parent, LLVMBasicBlockRef breakBlock, LLVMBasicBlockRef continueBlock, Scope scope)
{
    public LoopContext? Parent { get; } = parent;
    public Scope Scope { get; } = scope;

    public readonly LLVMBasicBlockRef BreakBlock = breakBlock;
    public readonly LLVMBasicBlockRef ContinueBlock = continueBlock;
}