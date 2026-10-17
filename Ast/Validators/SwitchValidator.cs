#nullable enable
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Validators;

internal static class SwitchValidator
{
    public static void Validate(AstSwitch astSwitch)
    {
        if (astSwitch.ExpressionNode == null)
        {
            var error = new BifySyntaxError(ErrorMessage.ConditionIsRequiredInSwitch(), "", astSwitch.Token.Value);
            Traceback.Instance.ThrowException(error, astSwitch.Token.Column);
        }
    }

    public static void ThrowExpectedColonAfterCase(Token token)
    {
        var error = new BifySyntaxError(ErrorMessage.ExpectedColonAfterCase(), "", token.Value);
        Traceback.Instance.ThrowException(error, token.Column);
    }

    public static void ThrowExpectedColonAfterDefault(Token token)
    {
        var error = new BifySyntaxError(ErrorMessage.ExpectedColonAfterDefault(), "", token.Value);
        Traceback.Instance.ThrowException(error, token.Column);
    }

    public static void ThrowMultipleDefaultError(Token token)
    {
        var error = new BifySyntaxError(ErrorMessage.MultipleDefaultInSwitch(), "", token.Value);
        Traceback.Instance.ThrowException(error, token.Column);
    }

    public static void ThrowInvalidSwitchBodyToken(Token token)
    {
        var error = new BifySyntaxError(ErrorMessage.InvalidSwitchBodyToken(token.Value), "", token.Value);
        Traceback.Instance.ThrowException(error, token.Column);
    }
}
