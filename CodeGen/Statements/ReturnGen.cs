#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Expressions;

namespace BifyLang.CodeGen.Statements;

public class ReturnGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public void EmitReturn(AstReturn astReturn)
    {
        if (astReturn.ArgumentsNode == null)
        {
            _ctx.Builder.BuildRetVoid();
            return;
        }

        var expr = new ExpressionDispatcher(_ctx);
        var returnValue = TypeCaster.ImplicitCast(_ctx, expr.EmitExpression(astReturn.ArgumentsNode), _ctx.FunctionReturnType);
        _ctx.Builder.BuildRet(returnValue.Value);
    }
}