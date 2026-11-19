using BifyLang.Ast;

namespace BifyLang.CodeGen.Core.Class;

public record ClassInfo(
    string Name,
    AstClass ClassNode,
    string ModuleName,
    string MangledName

)
{
    public int MinType { get; set; }
    public int MaxType { get; set; }
    public sealed override string ToString()
    {
        return $"{Name} ({MangledName}) [Module: {ModuleName}, TypeId: {MinType}..{MaxType}]";
    }
}
