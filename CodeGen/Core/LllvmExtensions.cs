using System;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public static class LllvmExtensions
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
}