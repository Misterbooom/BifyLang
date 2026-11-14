using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Validators
{
    internal class UnaryOperatorValidator
    {
        public static bool Validate(AstBuilder builder, string op, Token identifierToken)
        {
            if (identifierToken == null)
            {
                builder.Fail(ErrorMessage.MissingOperandAfterUnaryOperator(op), DiagnosticKind.Syntax, null);
            }
            else if (identifierToken.Type != TokenType.IDENTIFIER)
            {
                builder.Fail(ErrorMessage.OperandMustBeIdentifierAfterUnaryOperator(op), DiagnosticKind.Syntax, identifierToken);
            }
            return true;
        }
    }
}
