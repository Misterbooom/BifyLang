using BifyLang.Ast;

namespace BifyLang.CodeGen.Core;

public record ClassInfo
(
    string Name,
    AstClass ClassNode,
    string ModuleName
);