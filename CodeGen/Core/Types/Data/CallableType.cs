using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Types.Data;

public class CallableType(
    string name,
    string fullName,
    ParameterInfo[] parametersType,
    BifyType returnType,
    LLVMTypeRef functionType,
    bool isMethod = false) : BifyType(name, functionType)
{
    public readonly string FullName = fullName;
    public readonly ParameterInfo[] ParametersType = parametersType;
    public readonly BifyType ReturnType = returnType;
    public readonly bool IsMethod = isMethod;
}