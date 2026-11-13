using System.Collections.Generic;
using BifyLang.Lexer;

namespace BifyLang.Ast
{
    public static class AstConfig
    {
        public static readonly Dictionary<TokenType, int> Precedence = new()
        {
            { TokenType.LPAREN,    0 },
            { TokenType.RPAREN,    0 },

            { TokenType.POW,       10 },
            { TokenType.MUL,       9 },
            { TokenType.DIV,       9 },
            { TokenType.FLOORDIV,  9 },
            { TokenType.MOD,       9 },
            { TokenType.ADD,       8 },
            { TokenType.SUB,       8 },

            { TokenType.GT,        7 },
            { TokenType.LT,        7 },
            { TokenType.GTEQ,      7 },
            { TokenType.LTEQ,      7 },
            { TokenType.EQ,        6 },
            { TokenType.NEQ,       6 },

            { TokenType.AND,       5 },
            { TokenType.OR,        4 },
            { TokenType.NOT,       3 },

            { TokenType.BITAND,    2 },
            { TokenType.BITOR,     2 },
            { TokenType.BITXOR,    2 },
            { TokenType.BITNOT,    1 },

            { TokenType.INCREMENT, 11 },
            { TokenType.DECREMENT, 11 },

            { TokenType.COMMA,     0 },
            { TokenType.RANGE,     0 },
        };

    }
}
