using System.Collections.Generic;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class ClassHandler(AstBuilder builder) : TokenHandler(builder)
    {
        public override void HandleToken(Token token)
        {
            bool isStruct = token.Value == "struct";
            string kindLower = isStruct ? "struct" : "class";
            string kindTitle = isStruct ? "Struct" : "Class";

            Token identifierToken = Builder.GetNextToken();
            if (identifierToken?.Type != TokenType.IDENTIFIER)
            {
                Builder.Fail($"Invalid {kindLower} name: '{identifierToken?.Value ?? "null"}'.", DiagnosticKind.Syntax, identifierToken ?? token);
            }
            AstNode nameNode = new AstIdentifier(identifierToken, identifierToken.Value);
            Builder.TokenIndex += 1;
            var nextToken = Builder.GetNextToken();
            AstNode parentNode = null;
            if (nextToken?.Type == TokenType.COLON)
            {
                List<Token> parentTokens = TokensFormatter.GetTokensBetween(
                    Builder.Tokens,
                    ref Builder.TokenIndex,
                    TokenType.COLON,
                    TokenType.LCUR,
                    Builder.Diagnostics,
                    Builder.FilePath
                );
                parentNode = Builder.ParseTokens(parentTokens);
                Builder.TokenIndex -= 1;
            }

            AstNode bodyNode = Builder.HandleBody(kindTitle);
            VerifyBody((AstBlock)bodyNode, kindLower, kindTitle);
            Builder.CurrentNode = isStruct
                ? new AstStruct(token, nameNode, bodyNode, parentNode)
                : new AstClass(token, nameNode, bodyNode, parentNode);
        }

        private void VerifyBody(AstBlock body, string kindLower, string kindTitle)
        {
            int methodsCount = 0;
            foreach (AstNode child in body.ChildNodes)
            {
                if (child is AstPoison)
                {
                    continue;
                }
                if (child is AstVarDecl)
                {
                    if (methodsCount > 0)
                    {
                        Builder.Error(
                            $"{kindTitle} attributes must be defined before methods in the {kindLower} body.",
                            DiagnosticKind.Syntax,
                            child.Token
                        );
                    }
                }
                else if (child is AstFunctionDecl)
                {
                    methodsCount++;
                }
                else
                {
                    Builder.Error(
                        $"Invalid statement in {kindLower} body. Only methods and variables are allowed. " +
                        $"Not a {child.GetType().Name.Replace("Ast", "").ToLower()}.",
                        DiagnosticKind.Syntax,
                        child.Token
                    );
                }
            }
        }
    }
}
