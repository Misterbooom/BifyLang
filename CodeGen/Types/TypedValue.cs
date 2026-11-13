using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Types;

public readonly struct TypedValue(LLVMValueRef value, BifyType type, bool isWeak = false, bool isCallable = false)
{
    public LLVMValueRef Value { get; } = value;
    public BifyType Type { get; } = type;
    public bool IsWeak { get; } = isWeak;
    public bool IsCallable { get; } = isCallable;
    public override string ToString()
    {
        return $"{Type}({Value})";
    }
}