#nullable enable
using System.Collections.Generic;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Types.Data;

public class ClassType(string name, string fullName, LLVMTypeRef pointerType, LLVMTypeRef structType, string moduleName, ClassType? parent = null) : BifyType(name, pointerType)
{
    public LLVMTypeRef StructType { get; set; } = structType;
    public readonly ClassType? Parent = parent;
    public LLVMValueRef VTableGlobal;
    public VTable VTable = new();
    public int MinType { get; init; }
    public int MaxType { get; init; }

    public Dictionary<string, ClassField> Fields = new();
    public Dictionary<string, ClassMethod> Methods = new();
    public readonly string FullName = fullName;
    public readonly string ModuleName =  moduleName;
    public bool IsSubclassOf(ClassType other)
    {
        var cur = this;
        while (cur != null)
        {
            if (cur.FullName == other.FullName) return true;
            cur = cur.Parent;
        }
        return false;
    }
}
