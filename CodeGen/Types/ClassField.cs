#nullable enable
using System.Collections.Generic;
using BifyLang.Ast;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Types;

public enum AccessLevel
{
    PUBLIC,
    PRIVATE,
    PROTECTED,
}

public struct ClassField(
    string name,
    BifyType type,
    AstNode? value,
    AccessLevel accessLevel,
    int index = -1,
    bool isConst = false,
    bool isWeak = false,
    string declaringClass = "")
{
    public readonly string Name = name;
    public readonly BifyType Type = type;
    public LLVMValueRef Value = default;
    public readonly int Index = index;
    public readonly AccessLevel AccessLevel = accessLevel;
    public readonly string DeclaringClass = declaringClass;
    public AstNode? ValueNode = value;
    public bool IsConst = isConst;
    public bool IsWeak = isWeak;
}

public struct ClassMethod(
    string name,
    BifyType signature,
    LLVMValueRef functionValue,
    AccessLevel accessLevel,
    int vtableIndex = -1,
    string declaringClass = "",
    AstNode? body = null,
    bool isVirtual = false,
    bool isOverride = false,
    List<ParameterInfo>? parameters = null)
{
    public readonly string Name = name;
    public readonly BifyType Signature = signature;
    public LLVMValueRef FunctionValue = functionValue;
    public int VTableIndex = vtableIndex;
    public readonly AccessLevel AccessLevel = accessLevel;
    public readonly string DeclaringClass = declaringClass;
    public readonly AstNode? Body = body;
    public bool IsVirtual = isVirtual;
    public bool IsOverride = isOverride;
    public List<ParameterInfo> Parameters = parameters ?? [];
}