using BifyLang.Exceptions;

namespace BifyLang.Ast.Validators
{
    internal class ConditionStatementValidator
    {
        public static void ThrowUnmatchedIfError(AstBuilder builder, AstElse astElse)
        {
            builder.Fail(ErrorMessage.ElseWithoutMatchingIf(), DiagnosticKind.Syntax, astElse.Token);
        }

        public static void ThrowUnmatchedIfError(AstBuilder builder, AstElseIf astElseIf)
        {
            builder.Fail(ErrorMessage.ElseIfWithoutMatchingIf(), DiagnosticKind.Syntax, astElseIf.Token);
        }

        public static void ThrowElseIfAfterElseError(AstBuilder builder, AstElseIf astElseIf)
        {
            builder.Fail(ErrorMessage.ElseIfCannotFollowElseDirectly(), DiagnosticKind.Syntax, astElseIf.Token);
        }
    }
}
