using BifyLang.Ast;

namespace BifyLang.CodeGen.Core.Class;

public record ClassInfo
(
    string Name,
    AstClass ClassNode,
    string ModuleName
);