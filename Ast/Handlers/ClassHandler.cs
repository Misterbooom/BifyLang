using System.Collections.Generic;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class ClassHandler(AstBuilder builder) : TokenHandler(builder)
    {
        public override void HandleToken(Token token)
        {
            Token identifierToken = Builder.GetNextToken();
            if (identifierToken?.Type != TokenType.IDENTIFIER)
            {
                new BifySyntaxError($"Invalid class name: '{identifierToken?.Value ?? "null"}'.").Throw();
            }
            AstNode nameNode = new AstIdentifier(identifierToken, identifierToken.Value);
            Builder.TokenIndex += 1;
            var nextToken = Builder.GetNextToken();
            AstNode parentNode = null;
            if (nextToken?.Type == TokenType.COLON)
            {
                List<Token> parentTokens = TokensFormatter.GetTokensBetween(Builder.Tokens, ref Builder.TokenIndex, TokenType.COLON, TokenType.LCUR);
                parentNode = Builder.ParseTokens(parentTokens);
                Builder.TokenIndex -= 1;
            }
          
         
            BifyDebug.Log($"NExt otken : {nextToken.Value}");
            AstNode bodyNode = Builder.HandleBody("Class");
            VerifyClassBody((AstBlock)bodyNode);
            Builder.CurrentNode = new AstClass(token, nameNode, bodyNode, parentNode);
        }
        private void VerifyClassBody(AstBlock body)
        {
            int methodsCount = 0;
            foreach (AstNode child in body.ChildNodes)
            {
                Traceback.Instance.SetCurrentLine(child.LineNumber);
                if (child is AstVarDecl)
                {
                    if (methodsCount > 0)
                        new BifySyntaxError("Class attributes must be defined before methods in the class body.").Throw();
                }
                else if (child is AstFunctionDecl)
                {
                    methodsCount++;
                }
                else
                {
                    new BifySyntaxError($"Invalid statement in class body. Only methods and variables are allowed. " +
                        $"Not a {child.GetType().Name.Replace("Ast", "").ToLower()}.").Throw();
                }
            }
        }
    }
}
