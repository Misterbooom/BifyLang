using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Validators
{
    internal class UnaryOperatorValidator
    {
        public static bool Validate(string op,Token identifierToken)
        {
            if (identifierToken == null)
            {
                BifySyntaxError error = new(ErrorMessage.MissingOperandAfterUnaryOperator(op),"",op);
                Traceback.Instance.ThrowException(error, identifierToken.Column);
            }
            else if (identifierToken.Type != TokenType.IDENTIFIER)
            {
                BifySyntaxError error = new(ErrorMessage.OperandMustBeIdentifierAfterUnaryOperator(op),"",op);
                Traceback.Instance.ThrowException(error,identifierToken.Column);

            }
            return true;

        }
    }
}
