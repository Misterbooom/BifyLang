#nullable enable
using System.Collections.Generic;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Types.Data;

public interface ICompositeType
{
    string Name { get; }
    string FullName { get; }
    string ModuleName { get; }
    LLVMTypeRef LlvmStructType { get; }
    Dictionary<string, ClassField> Fields { get; }
    Dictionary<string, ClassMethod> Methods { get; }
}
