#nullable enable
using System.Collections.Generic;
using BifyLang.Ast;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Types.Data;

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
    ICompositeType declaringType = null!,
    bool isPoison = false)
{
    public readonly string Name = name;
    public readonly BifyType Type = type;
    public LLVMValueRef Value = default;
    public readonly int Index = index;
    public readonly AccessLevel AccessLevel = accessLevel;
    public readonly ICompositeType DeclaringType = declaringType;
    public readonly AstNode? ValueNode = value;
    public readonly bool IsConst = isConst;
    public readonly bool IsWeak = isWeak;
    public readonly bool IsPoison = isPoison;
}

public struct ClassMethod(
    string name,
    CallableType signature,
    LLVMValueRef functionValue,
    AccessLevel accessLevel,
    int vtableIndex = -1,
    ICompositeType declaringType = null!,
    AstNode? body = null,
    bool isVirtual = false,
    bool isOverride = false,
    List<ParameterInfo>? parameters = null,
    bool isPoison = false)
{
    public readonly string Name = name;
    public readonly CallableType Signature = signature;
    public LLVMValueRef FunctionValue = functionValue;
    public int VTableIndex = vtableIndex;
    public readonly AccessLevel AccessLevel = accessLevel;
    public readonly ICompositeType DeclaringType = declaringType;
    public readonly AstNode? Body = body;
    public readonly bool IsVirtual = isVirtual;
    public readonly bool IsOverride = isOverride;
    public readonly List<ParameterInfo> Parameters = parameters ?? [];
    public readonly bool IsPoison = isPoison;
}