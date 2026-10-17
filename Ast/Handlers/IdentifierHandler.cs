using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class IdentifierHandler(AstBuilder builder) : TokenHandler(builder)
    {
        public override void HandleToken(Token token)
        {
            HandleExpression(token);
        }

        private void HandleExpression(Token token)
        {
            Builder.CurrentNode = new BinaryOperatorHandler(Builder).ParsePrimary();
        }

        public AstNode ParseIdentifier(Token token, bool isAlreadyNextToken = false)
        {
            if (!isAlreadyNextToken)
            {
                Builder.NextToken();
            }

            return new AstIdentifier(token, token.Value);
        }
    }
}