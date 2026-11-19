#nullable enable
using BifyLang.Ast;

namespace BifyLang.CodeGen.Core.Types.Data;

public record ParameterInfo(
    string Name,
    BifyType Type,
    bool IsConst,
    AstNode? Value
);
