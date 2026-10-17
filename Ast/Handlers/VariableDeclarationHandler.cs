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
                new BifySyntaxError("Invalid variable declaration syntax. Expected `Type name` or `Type name = value`.").Throw();
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

            if (!Builder.IsAtEnd())
            {
                assignToken = Builder.Consume(TokenType.ASSIGN, "Expected '=' after variable name.");
                valueTokens = Builder.Tokens[Builder.TokenIndex..];
                if (valueTokens.Count == 0)
                {
                    new BifySyntaxError(ErrorMessage.EmptyValueAssigned()).Throw();
                }
                valueNode = Builder.ParseTokens(valueTokens);
                if (valueNode == null)
                {
                    new BifySyntaxError(ErrorMessage.EmptyValueAssigned()).Throw();
                }
            }
            Builder.MoveToEnd();

            VariableDeclarationValidator.Validate(header.NameNode, header.TypeNode, valueNode, valueTokens, header.FlagNode, assignToken);
            AstAssignment astAssignment = new(assignToken, header.NameNode, valueNode);
            Builder.Nodes.Clear();
            Builder.CurrentNode = new AstVarDecl(assignToken, astAssignment, header.TypeNode, header.FlagNode);
        }
    }
}
