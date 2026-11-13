using BifyLang.Lexer;

namespace BifyLang.Ast;

internal readonly struct TypeSuffix(Token token, int openIndex, int closeIndex)
{
    public readonly Token Token = token;
    public readonly int OpenIndex = openIndex;   
    public readonly int CloseIndex = closeIndex; 
    public bool IsPointer => Token.Type is TokenType.MUL or TokenType.POINTER;
}
