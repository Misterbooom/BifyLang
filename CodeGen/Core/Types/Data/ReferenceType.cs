#nullable enable
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Types.Data;

public abstract class ReferenceType(string name, LLVMTypeRef type) : BifyType(name, type)
{
    public override bool IsReference => true;
}
