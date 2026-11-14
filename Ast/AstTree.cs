using System.Collections.Generic;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast
{
    internal class AstTree
    {
        private readonly string[] _sourceCode;
        private readonly string _filePath;

        public DiagnosticBag Diagnostics { get; }

        public AstTree(string[] sourceCode, DiagnosticBag? diagnostics = null, string filePath = "main")
        {
            _sourceCode = sourceCode;
            Diagnostics = diagnostics ?? new DiagnosticBag();
            _filePath = string.IsNullOrEmpty(filePath) ? "main" : filePath;
        }

        public AstModule ParseTokens(List<Token> tokens)
        {
            List<AstNode> lines = [];
            List<List<Token>> splitLines;
            try
            {
                splitLines = TokensFormatter.SplitLines(tokens, Diagnostics, _filePath);
            }
            catch (BifyCompilationException)
            {
                Token fallback = tokens.Count > 0 ? tokens[^1] : new Token(TokenType.POISON, "");
                lines.Add(new AstPoison(fallback));
                return CreateModule(lines);
            }

            foreach (var lineTokens in splitLines)
            {
                try
                {
                    AstNode? node = ParseLine(lineTokens);
                    if (node != null)
                    {
                        lines.Add(node);
                    }
                }
                catch (BifyCompilationException)
                {
                    Token fallback = lineTokens.Count > 0 ? lineTokens[0] : new Token(TokenType.POISON, "");
                    lines.Add(new AstPoison(fallback));
                }
            }

            return CreateModule(lines);
        }

        private AstModule CreateModule(List<AstNode> lines)
        {
            return new AstModule("", _filePath, lines)
            {
                SourceCode = _sourceCode
            };
        }

        private AstNode ParseLine(List<Token> line)
        {
            AstBuilder builder = new(line, Diagnostics, _filePath);
            return builder.BuildNode();
        }
    }
}