#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Types;

namespace BifyLang.CodeGen.Expressions;

public class CastGen
{
    public static TypedValue EmitCast(CodeGenContext ctx, ExpressionDispatcher dispatcher, AstCast castNode)
    {
        BifyType targetType = TypeResolver.Resolve(castNode.TypeNode, ctx);
        TypedValue value = dispatcher.EmitExpression(castNode.ValueNode);

        return TypeCaster.ExplicitCast(ctx, value, targetType);
    }
}