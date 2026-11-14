using System.Collections.Generic;
using BifyLang.Ast.Validators;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class VariableDeclarationHandler(AstBuilder builder) : TokenHandler(builder)
    {
        public override void HandleToken(Token token)
        {
            if (!DeclarationParser.TryParseHeader(Builder, out DeclarationHeader header) ||
                header.Kind != DeclarationKind.Variable)
            {
                Builder.Fail(
                    "Invalid variable declaration syntax. Expected `Type name` or `Type name = value`.",
                    DiagnosticKind.Syntax,
                    token
                );
                return;
            }
            ParseDeclaration(header);
        }

        public void ParseDeclaration(DeclarationHeader header)
        {
            Builder.TokenIndex = header.EndIndex;
            Token nameToken = header.NameNode.Token;

            Token assignToken = new(TokenType.ASSIGN, "=") { Line = nameToken.Line, Column = nameToken.Column };
            AstNode valueNode = null;
            List<Token> valueTokens = [];
            bool isPoison = false;

            if (!Builder.IsAtEnd())
            {
                try
                {
                    assignToken = Builder.Consume(TokenType.ASSIGN, "Expected '=' after variable name.");
                    valueTokens = Builder.Tokens[Builder.TokenIndex..];
                    if (valueTokens.Count == 0)
                    {
                        Builder.Fail(ErrorMessage.EmptyValueAssigned(), DiagnosticKind.Syntax, assignToken);
                    }
                    valueNode = Builder.ParseTokens(valueTokens);
                    if (valueNode == null)
                    {
                        Builder.Fail(ErrorMessage.EmptyValueAssigned(), DiagnosticKind.Syntax, assignToken);
                    }
                    if (valueNode.IsPoison)
                    {
                        isPoison = true;
                    }
                }
                catch (BifyCompilationException)
                {
                    valueNode = new AstPoison(assignToken);
                    isPoison = true;
                }
            }
            Builder.MoveToEnd();

            if (!isPoison)
            {
                try
                {
                    VariableDeclarationValidator.Validate(
                        Builder,
                        header.NameNode,
                        header.TypeNode,
                        valueNode,
                        valueTokens,
                        header.FlagNode,
                        assignToken
                    );
                }
                catch (BifyCompilationException)
                {
                    valueNode = new AstPoison(assignToken);
                    isPoison = true;
                }
            }

            AstAssignment astAssignment = new(assignToken, header.NameNode, valueNode);
            Builder.Nodes.Clear();
            Builder.CurrentNode = new AstVarDecl(assignToken, astAssignment, header.TypeNode, header.FlagNode)
            {
                IsPoison = isPoison
            };
        }
    }
}
