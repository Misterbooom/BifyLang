using System.Collections.Generic;
using BifyLang.Lexer;

namespace BifyLang.Ast;

internal enum DeclarationKind
{
    Variable,
    Function
}

public readonly record struct HeaderMatch(
    DeclarationKind Kind,
    List<Token> Flags,
    List<Token> TypeParts,
    List<TypeSuffix> Suffixes,
    Token NameToken,
    int EndIndex,
    bool IsCtorOrDtor = false
);
