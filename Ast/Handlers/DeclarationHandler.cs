using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{

    internal class DeclarationHandler(AstBuilder builder, DeclarationHeader header) : TokenHandler(builder)
    {
        public override void HandleToken(Token token)
        {
            switch (header.Kind)
            {
                case DeclarationKind.Function:
                    new FunctionHandler(Builder).ParseDeclaration(header);
                    break;
                default:
                    new VariableDeclarationHandler(Builder).ParseDeclaration(header);
                    break;
            }
        }
    }
}
