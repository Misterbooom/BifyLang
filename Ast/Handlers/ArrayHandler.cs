using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class ArrayHandler(AstBuilder builder):TokenHandler(builder)
    {
        public override void HandleToken(Token token)
        {
            Builder.CurrentNode = GetArrayNode(token);
        }
        public AstNode GetArrayNode(Token token)
        {
            Builder.TokenIndex -= 1;
            var tokensInBrackets = TokensFormatter.GetTokensBetween(
                Builder.Tokens,
                ref Builder.TokenIndex,
                TokenType.LBRACKET,
                TokenType.RBRACKET,
                Builder.Diagnostics,
                Builder.FilePath
            );

            AstNode valueNode = Builder.ParseTokens(tokensInBrackets);

            Builder.TokenIndex++;
            return new AstArray(token, valueNode);
        }
    }
}
