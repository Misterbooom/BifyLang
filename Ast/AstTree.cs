using System.Collections.Generic;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast
{
    internal class AstTree
    {
        private readonly string[] _sourceCode;
        public AstTree(string[] sourceCode)
        {
            _sourceCode = sourceCode;
            Traceback.Instance.InitializeSource(sourceCode);
        }

        public AstModule ParseTokens(List<Token> tokens)
        {
            int tokenIndex = 0;
            int lineCount = 0;
            List<AstNode> lines = [];
            foreach(var lineTokens in TokensFormatter.SplitLines(tokens))
            {
                lines.Add(ParseLine(lineTokens, ref tokenIndex, ref lineCount));
            }
            AstModule module = new("", Traceback.Instance.FilePath, lines)
            {
                SourceCode = _sourceCode
            };
            return module;
        }
        private AstNode ParseLine(List<Token> line, ref int tokenIndex, ref int lineCount)
        {
            AstBuilder builder = new(line);
            return builder.BuildNode();
        }

    }
}