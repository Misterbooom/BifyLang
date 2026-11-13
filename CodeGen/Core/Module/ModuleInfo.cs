using BifyLang.Ast;

namespace BifyLang.CodeGen.Core.Module;

public record ModuleInfo
(
    string Name,
    string FullPath,
    AstModule ModuleNode,
    ModuleContext ModuleContext
);