using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal abstract class TokenHandler(AstBuilder builder)
    {
        protected readonly AstBuilder Builder = builder;

        public abstract void HandleToken(Token token);
    }
}
