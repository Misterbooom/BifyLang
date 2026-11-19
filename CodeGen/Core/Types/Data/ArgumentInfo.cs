using BifyLang.CodeGen.Core.Types.Service;

namespace BifyLang.CodeGen.Core.Types.Data;

public record ArgumentInfo(
    string Name,
    TypedValue Value,
    bool isPositional,
    int Position = -1
);
