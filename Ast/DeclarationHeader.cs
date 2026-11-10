namespace BifyLang.Ast;

internal enum DeclarationKind
{
    Variable,
    Function
}

internal sealed class DeclarationHeader
{
    public required DeclarationKind Kind { get; init; }
    public required AstFlag FlagNode { get; init; }
    public required AstNode TypeNode { get; init; }
    public required AstIdentifier NameNode { get; init; }
    public required int EndIndex { get; init; }
}
