#nullable enable
namespace BifyLang.CodeGen.Types;

public record ParameterInfo(
    string Name,
    BifyType Type,
    bool IsConst
);
