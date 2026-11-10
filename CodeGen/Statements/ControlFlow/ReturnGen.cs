#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Expressions;
using BifyLang.CodeGen.Types;

namespace BifyLang.CodeGen.Statements;

public class ReturnGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public void EmitReturn(AstReturn astReturn)
    {
        TypedValue? returnValue = null;
        if (astReturn.ArgumentsNode != null)
        {
            var expr = new ExpressionDispatcher(_ctx);
            returnValue = TypeCaster.ImplicitCast(_ctx, expr.EmitExpression(astReturn.ArgumentsNode), _ctx.FunctionReturnType);
            if (returnValue.Value.Type is ClassType)
            {
                if (!returnValue.Value.IsOwned)
                    _ctx.HeapControl.BuildRetainStrong(returnValue.Value.Value);
            }
        }

        var cur = _ctx.CurrentScope;
        while (cur != null)
        {
            cur.ReleaseLocals(_ctx);
            cur = cur.Parent;
        }

        if (!returnValue.HasValue)
            _ctx.Builder.BuildRetVoid();
        else
            _ctx.Builder.BuildRet(returnValue.Value.Value);
    }
}