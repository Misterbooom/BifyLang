using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class UnsafeHandler(AstBuilder builder):TokenHandler(builder)
    {
        public override void HandleToken(Token token)
        {
            AstBlock bodyNode = Builder.HandleBody("Unsafe");
            Builder.CurrentNode = new AstUnsafe(token, bodyNode);
        }
        
    }
}