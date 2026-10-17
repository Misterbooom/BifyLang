using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Types;

public readonly struct TypedValue(LLVMValueRef value, BifyType type)
{
    public LLVMValueRef Value { get; } = value;
    public BifyType Type { get; } = type;
    public override string ToString()
    {
        return $"{Type}({Value})";
    }
}