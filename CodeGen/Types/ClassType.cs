#nullable enable
using System.Collections.Generic;
using BifyLang.CodeGen.Core;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Types;

public class ClassType(string name, string fullName, LLVMTypeRef pointerType, LLVMTypeRef structType, ClassType? parent = null) : BifyType(name, pointerType)
{
    public LLVMTypeRef StructType { get; set; } = structType;
    public readonly ClassType? Parent = parent;
    public LLVMValueRef VTableGlobal;
    public VTable VTable = new();
    public Dictionary<string, ClassField> Fields = new();
    public Dictionary<string, ClassMethod> Methods = new();
    public readonly string FullName = fullName;
    public bool IsSubclassOf(ClassType other)
    {
        var cur = this;
        while (cur != null)
        {
            if (cur.Name == other.Name) return true;
            cur = cur.Parent;
        }
        return false;
    }
}