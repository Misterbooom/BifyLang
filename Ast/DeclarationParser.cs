using System.Collections.Generic;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast;

internal static class DeclarationParser
{
        public static bool TryParseHeader(AstBuilder builder, out DeclarationHeader header)
        {
            header = null;
            List<Token> tokens = builder.Tokens;
            int i = builder.TokenIndex;
            List<List<Token>> words = CollectWords(tokens, ref i);

            if (i < tokens.Count && tokens[i].Type is TokenType.CONSTRUCTOR or TokenType.DESTRUCTOR)
                return TryParseCtorHeader(tokens, ref i, words, out header);

            if (words.Count == 0 || !CollectSuffixes(tokens, ref i, out var suffixes))
                return false;

            if (!ResolveNameAndType(tokens, ref i, words, suffixes, out var typeParts, out var nameToken, out int flagCount))
                return false;

            if (!DetermineKind(tokens, i, out var kind))
                return false;

            List<AstNode> flags = [];
            for (int f = 0; f < flagCount; f++)
                flags.Add(new AstIdentifier(words[f][0], words[f][0].Value));

            header = new DeclarationHeader
            {
                Kind = kind,
                FlagNode = new AstFlag(nameToken, flags),
                TypeNode = BuildTypeNode(builder, typeParts, suffixes),
                NameNode = new AstIdentifier(nameToken, nameToken.Value),
                EndIndex = i
            };
            return true;
        }

        private static List<List<Token>> CollectWords(List<Token> tokens, ref int i)
        {
            List<List<Token>> words = [];
            while (i < tokens.Count && tokens[i].Type is TokenType.IDENTIFIER or TokenType.CONST)
            {
                List<Token> parts = [tokens[i++]];
                if (parts[0].Type == TokenType.IDENTIFIER)
                {
                    while (i + 1 < tokens.Count && tokens[i].Type == TokenType.DOT && tokens[i + 1].Type == TokenType.IDENTIFIER)
                    {
                        parts.Add(tokens[i++]);
                        parts.Add(tokens[i++]);
                    }
                }
                words.Add(parts);
            }
            return words;
        }

        private static bool TryParseCtorHeader(List<Token> tokens, ref int i, List<List<Token>> words, out DeclarationHeader? header)
        {
            header = null;
            if (i >= tokens.Count || tokens[i].Type is not (TokenType.CONSTRUCTOR or TokenType.DESTRUCTOR))
                return false;

            Token ctorToken = tokens[i++];
            if (i >= tokens.Count || tokens[i].Type != TokenType.LPAREN)
                return false;

            int close = FindClosing(tokens, i, TokenType.LPAREN, TokenType.RPAREN);
            if (close < 0 || close + 1 >= tokens.Count || tokens[close + 1].Type != TokenType.LCUR)
                return false;

            List<AstNode> ctorFlags = [];
            foreach (var w in words)
                if (w.Count > 1) return false;
                else ctorFlags.Add(new AstIdentifier(w[0], w[0].Value));

            var voidTok = new Token(TokenType.IDENTIFIER, "void") { Line = ctorToken.Line, Column = ctorToken.Column };
            header = new DeclarationHeader
            {
                Kind = DeclarationKind.Function,
                FlagNode = new AstFlag(ctorToken, ctorFlags),
                TypeNode = new AstIdentifier(voidTok, "void"),
                NameNode = new AstIdentifier(ctorToken, ctorToken.Value),
                EndIndex = i
            };
            return true;
        }

        private static bool CollectSuffixes(List<Token> tokens, ref int i, out List<TypeSuffix> suffixes)
        {
            suffixes = [];
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
                    if (close < 0) return false;
                    suffixes.Add(new TypeSuffix(t, i, close));
                    i = close + 1;
                }
                else break;
            }
            return true;
        }

        private static bool ResolveNameAndType(
            List<Token> tokens, ref int i, List<List<Token>> words, List<TypeSuffix> suffixes,
            out List<Token> typeParts, out Token nameToken, out int flagCount)
        {
            typeParts = [];
            nameToken = default;
            flagCount = 0;

            if (suffixes.Count > 0)
            {
                if (i >= tokens.Count || tokens[i].Type != TokenType.IDENTIFIER)
                    return false;
                nameToken = tokens[i++];
                typeParts = words[^1];
                flagCount = words.Count - 1;
            }
            else
            {
                if (words.Count < 2 || words[^1].Count > 1) return false;
                nameToken = words[^1][0];
                typeParts = words[^2];
                flagCount = words.Count - 2;
            }

            for (int f = 0; f < flagCount; f++)
                if (words[f].Count > 1) return false;

            return typeParts[0].Type == TokenType.IDENTIFIER && nameToken.Type == TokenType.IDENTIFIER;
        }

        private static bool DetermineKind(List<Token> tokens, int i, out DeclarationKind kind)
        {
            kind = DeclarationKind.Variable;
            if (i >= tokens.Count || tokens[i].Type == TokenType.ASSIGN)
                return true;

            if (tokens[i].Type == TokenType.LPAREN)
            {
                int close = FindClosing(tokens, i, TokenType.LPAREN, TokenType.RPAREN);
                if (close < 0 || close + 1 >= tokens.Count || tokens[close + 1].Type != TokenType.LCUR)
                    return false;
                kind = DeclarationKind.Function;
                return true;
            }
            return false;
        }

        private static AstNode BuildTypeNode(AstBuilder builder, List<Token> typeParts, List<TypeSuffix> suffixes)
        {
            AstNode typeNode = new AstIdentifier(typeParts[0], typeParts[0].Value);
            for (int p = 1; p + 1 < typeParts.Count; p += 2)
                typeNode = new AstMemberAccess(typeParts[p], typeNode, new AstIdentifier(typeParts[p + 1], typeParts[p + 1].Value));

            foreach (TypeSuffix suffix in suffixes)
            {
                if (suffix.IsPointer)
                {
                    Token pointerToken = new(TokenType.POINTER, "*")
                    {
                        Line = suffix.Token.Line, Column = suffix.Token.Column
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
                if (tokens[i].Type == open) depth++;
                else if (tokens[i].Type == close && --depth == 0) return i;
            }
            return -1;
        }
    }
