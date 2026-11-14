using System.Collections.Generic;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Validators
{
    internal static class VariableDeclarationValidator
    {
        public static void Validate(AstBuilder builder, AstNode identifierNode, AstNode typeNode,
            AstNode valueNode, List<Token> valueTokens, AstNode flagNode, Token assignmentToken)
        {
            BifyDebug.Assert(identifierNode != null && typeNode != null, "identifierNode or typeNode is null");
            if (identifierNode is not AstIdentifier)
            {
                builder.Fail(
                    ErrorMessage.InvalidVariableName(identifierNode?.Token.Value),
                    DiagnosticKind.Name,
                    identifierNode?.Token ?? assignmentToken
                );
            }

            if (typeNode != null && (typeNode is AstIdentifier or AstIndexOperator or AstMemberAccess ||
                                     typeNode.Token.Type == TokenType.POINTER))
            {
                return;
            }

            builder.Fail(
                ErrorMessage.InvalidVariableType(typeNode?.Token.Value),
                DiagnosticKind.Type,
                typeNode?.Token ?? assignmentToken
            );
        }
    }

    internal class AssignmentOperatorValidator
    {
        public static bool Validate(AstBuilder builder, Token variableToken, Token assignmentToken, List<Token> valueTokens)
        {
            if (valueTokens.Count == 0)
            {
                builder.Fail(ErrorMessage.EmptyValueAssigned(), DiagnosticKind.Syntax, assignmentToken);
            }
            if (variableToken != null && (variableToken.Type == TokenType.IDENTIFIER || variableToken.Type == TokenType.INDEX_OPERATOR))
            {
                if (assignmentToken != null)
                {
                    return true;
                }
                builder.Fail(ErrorMessage.MissingAssignmentOperator(), DiagnosticKind.Syntax, variableToken);
            }
            else
            {
                builder.Fail(ErrorMessage.InvalidVariableName(variableToken?.Value), DiagnosticKind.Syntax, assignmentToken);
            }

            return false;
        }
    }


}
