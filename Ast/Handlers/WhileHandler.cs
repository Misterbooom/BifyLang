using BifyLang.Ast.Validators;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class WhileHandler(AstBuilder builder) : TokenHandler(builder)
    {
        public override void HandleToken(Token token)
        {
            AstNode conditionNode = Builder.ParseTokens(Builder.GetConditionTokens());
            AstNode blockNode = Builder.HandleBody("While");
            AstWhile astWhile = new AstWhile(token, blockNode, conditionNode);
            LoopValidator.ValidateWhileStatement(Builder, astWhile);

            Builder.CurrentNode = astWhile;
            Builder.MoveToEnd();
        }

        
    }
}
