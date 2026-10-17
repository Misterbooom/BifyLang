#nullable enable
using System.Collections.Generic;
using BifyLang.CodeGen.Declarations.ClassHandler;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Types;

public class ClassType(string name, LLVMTypeRef pointerType, LLVMTypeRef structType, ClassType? parent = null) : BifyType(name, pointerType)
{
    public LLVMTypeRef StructType { get; set; } = structType;
    public readonly ClassType? Parent = parent;
    public LLVMValueRef VTableGlobal;
    public LLVMTypeRef VTableType;
    public List<string> VTableMethodNames = [];
    public Dictionary<string, ClassField> Fields = new Dictionary<string, ClassField>();

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