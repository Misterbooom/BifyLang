using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Expressions;

public class ExpressionDispatcher(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public TypedValue EmitExpression(AstNode node)
    {
        return node switch
        {
            AstInteger integer => LiteralGen.EmitNumber(_ctx, integer),
            AstFloat floatNode => LiteralGen.EmitFloat(_ctx, floatNode),
            AstBoolean boolean => LiteralGen.EmitBool(_ctx, boolean),
            AstString str => LiteralGen.EmitString(_ctx, str),
            AstNull nullNode => LiteralGen.EmitNull(_ctx, nullNode),
            AstIdentifier identifier => IdentifierGen.EmitIdentifier(_ctx, identifier),
            AstBinaryOp binaryOp => BinaryOpGen.EmitBinaryOp(_ctx, this, binaryOp),
            AstCast cast => CastGen.EmitCast(_ctx, this, cast),
            AstUnaryOperator unaryOperator => new UnaryOperatorGen(_ctx).EmitUnaryOperator(unaryOperator),
            AstCall astCall => new CallGen(_ctx).EmitCall(astCall),
            _ => new BifySyntaxError($"'{node.Token.Value}' is not an expression").Throw<TypedValue>(),
        };
    }
}