using System.Linq;
using BifyLang.Ast.Validators;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class ForHandler(AstBuilder builder):TokenHandler(builder)
    {
        public override void HandleToken(Token token)
        {
            Builder.TokenIndex++;
            var splittedTokens = TokensFormatter.SplitTokensByType(Builder.GetConditionTokens(),TokenType.SEMICOLON);

            var initTokens = splittedTokens.ElementAtOrDefault(0);
            AstNode initNode = initTokens != null && initTokens.Count > 0
                ? new AstBuilder(initTokens, true).BuildNode()
                : null;

            AstNode conditionNode = Builder.ParseTokens(splittedTokens.ElementAtOrDefault(1));

            AstNode incrementNode = Builder.ParseTokens(splittedTokens.ElementAtOrDefault(2));
            if (Builder.GetNextToken()?.Type != TokenType.LCUR)

            {
                new BifySyntaxError("For body not found!").Throw();
            }
            var blockNode = Builder.HandleBody("For");
            Traceback.Instance.SetCurrentLine(token.Line);
            AstFor astFor = new AstFor(token, blockNode, conditionNode, incrementNode,initNode);
            LoopValidator.ValidateForStatement(splittedTokens,astFor);
            Builder.MoveToEnd();
            Builder.CurrentNode = astFor;



        }
    }
}
