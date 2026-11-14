#nullable enable
namespace BifyLang.CodeGen.Core.Types.Data;

public record ParameterInfo(
    string Name,
    BifyType Type,
    bool IsConst
);
