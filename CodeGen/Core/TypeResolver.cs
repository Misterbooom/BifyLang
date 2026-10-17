#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.CodeGen.Core;

public class TypeResolver
{
    public static BifyType Resolve(AstNode? typeNode, CodeGenContext ctx)
    {
        if (typeNode == null)
            return ctx.TypeRegistry.GetBifyType("void");

        return typeNode switch
        {
            AstIdentifier identifier => ctx.TypeRegistry.GetBifyType(identifier.Name),
            AstUnaryOperator unary when unary.Token.Type is TokenType.POINTER or TokenType.MUL =>
                ResolvePointer(unary, ctx),
            AstIndexOperator indexOp =>
                ResolveArray(indexOp, ctx),
            _ => new BifyUnknownError($"{typeNode.GetType()} is not a known type.").Throw<BifyType>()
        };
    }

    private static BifyType ResolvePointer(AstUnaryOperator unary, CodeGenContext ctx)
    {
        var baseType = Resolve(unary.Operand, ctx);
        string ptrName = $"{baseType.Name}*";
        return ctx.TypeRegistry.GetBifyType(ptrName);
    }

    private static BifyType ResolveArray(AstIndexOperator indexOp, CodeGenContext ctx)
    {
        var elemType = Resolve(indexOp.TargetNode, ctx);
        string arrName = indexOp.IndexNode is AstInteger num
            ? $"{elemType.Name}[{num.Value}]"
            : $"{elemType.Name}[]";
        return ctx.TypeRegistry.GetBifyType(arrName);
    }
}