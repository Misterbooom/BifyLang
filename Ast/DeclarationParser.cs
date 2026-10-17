using System.Collections.Generic;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast
{
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

  
    internal static class DeclarationParser
    {
        private readonly struct TypeSuffix(Token token, int openIndex, int closeIndex)
        {
            public readonly Token Token = token;
            public readonly int OpenIndex = openIndex;   
            public readonly int CloseIndex = closeIndex; 
            public bool IsPointer => Token.Type is TokenType.MUL or TokenType.POINTER;
        }

      
        public static bool TryParseHeader(AstBuilder builder, out DeclarationHeader header)
        {
            header = null;
            List<Token> tokens = builder.Tokens;
            int i = builder.TokenIndex;

            List<Token> words = [];
            while (i < tokens.Count && tokens[i].Type is TokenType.IDENTIFIER or TokenType.CONST)
            {
                words.Add(tokens[i++]);
            }

            if (i < tokens.Count && tokens[i].Type == TokenType.CONSTRUCTOR)
            {
                Token ctorToken = tokens[i++];
                if (i < tokens.Count && tokens[i].Type == TokenType.LPAREN)
                {
                    int close = FindClosing(tokens, i, TokenType.LPAREN, TokenType.RPAREN);
                    if (close >= 0 && close + 1 < tokens.Count && tokens[close + 1].Type == TokenType.LCUR)
                    {
                        List<AstNode> ctorFlags = [];
                        foreach (var w in words)
                        {
                            ctorFlags.Add(new AstIdentifier(w, w.Value));
                        }

                        header = new DeclarationHeader
                        {
                            Kind = DeclarationKind.Function,
                            FlagNode = new AstFlag(ctorToken, ctorFlags),
                            TypeNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "void")
                            {
                                Line = ctorToken.Line,
                                Column = ctorToken.Column
                            }, "void"),
                            NameNode = new AstIdentifier(ctorToken, ctorToken.Value),
                            EndIndex = i
                        };
                        return true;
                    }
                }
                return false;
            }

            if (words.Count == 0)
                return false;

            List<TypeSuffix> suffixes = [];
            while (i < tokens.Count)
            {
                Token t = tokens[i];
                if (t.Type is TokenType.MUL or TokenType.POINTER)
                {
                    suffixes.Add(new TypeSuffix(t, -1, -1));
                    i++;
                }
                else if (t.Type == TokenType.LBRACKET)
                {
                    int close = FindClosing(tokens, i, TokenType.LBRACKET, TokenType.RBRACKET);
                    if (close < 0)
                        return false;
                    suffixes.Add(new TypeSuffix(t, i, close));
                    i = close + 1;
                }
                else
                {
                    break;
                }
            }

            Token typeToken;
            Token nameToken;
            int flagCount;
            if (suffixes.Count > 0)
            {
                if (i >= tokens.Count || tokens[i].Type != TokenType.IDENTIFIER)
                    return false;
                nameToken = tokens[i++];
                typeToken = words[^1];
                flagCount = words.Count - 1;
            }
            else
            {
                if (words.Count < 2)
                    return false;
                nameToken = words[^1];
                typeToken = words[^2];
                flagCount = words.Count - 2;
            }

            if (typeToken.Type != TokenType.IDENTIFIER || nameToken.Type != TokenType.IDENTIFIER)
                return false;

            DeclarationKind kind;
            if (i >= tokens.Count || tokens[i].Type == TokenType.ASSIGN)
            {
                kind = DeclarationKind.Variable;
            }
            else if (tokens[i].Type == TokenType.LPAREN)
            {
                int close = FindClosing(tokens, i, TokenType.LPAREN, TokenType.RPAREN);
                if (close < 0 || close + 1 >= tokens.Count || tokens[close + 1].Type != TokenType.LCUR)
                    return false;
                kind = DeclarationKind.Function;
            }
            else
            {
                return false;
            }

            List<AstNode> flags = [];
            for (int f = 0; f < flagCount; f++)
            {
               
                flags.Add(new AstIdentifier(words[f], words[f].Value));
            }
            
            header = new DeclarationHeader
            {
                Kind = kind,
                FlagNode = new AstFlag(nameToken, flags),
                TypeNode = BuildTypeNode(builder, typeToken, suffixes),
                NameNode = new AstIdentifier(nameToken, nameToken.Value),
                EndIndex = i
            };
            return true;
        }

  
        private static AstNode BuildTypeNode(AstBuilder builder, Token typeToken, List<TypeSuffix> suffixes)
        {
            AstNode typeNode = new AstIdentifier(typeToken, typeToken.Value);
            foreach (TypeSuffix suffix in suffixes)
            {
                if (suffix.IsPointer)
                {
                    Token pointerToken = new(TokenType.POINTER, "*")
                    {
                        Line = suffix.Token.Line,
                        Column = suffix.Token.Column
                    };
                    typeNode = new AstUnaryOperator(pointerToken, typeNode, true);
                }
                else
                {
                    List<Token> sizeTokens = builder.Tokens[(suffix.OpenIndex + 1)..suffix.CloseIndex];
                    AstNode sizeNode = sizeTokens.Count == 0 ? null : builder.ParseTokens(sizeTokens);
                    typeNode = new AstIndexOperator(sizeNode, typeNode);
                }
            }
            return typeNode;
        }

        private static int FindClosing(List<Token> tokens, int openIndex, TokenType open, TokenType close)
        {
            int depth = 0;
            for (int i = openIndex; i < tokens.Count; i++)
            {
                if (tokens[i].Type == open)
                {
                    depth++;
                }
                else if (tokens[i].Type == close)
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
            return -1;
        }
    }
}
