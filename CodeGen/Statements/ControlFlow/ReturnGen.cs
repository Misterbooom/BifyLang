#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Expressions;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Statements.ControlFlow;

public class ReturnGen(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public void EmitReturn(AstReturn astReturn)
    {
        TypedValue? returnValue = null;

        if (astReturn.ArgumentsNode != null)
        {
            var expr = new ExpressionDispatcher(_ctx);
            using (_ctx.HeapControl.BeginTempScope())
            {
                returnValue = TypeCaster.ImplicitCast(_ctx, expr.EmitExpression(astReturn.ArgumentsNode),
                    _ctx.FunctionReturnType);
                if (returnValue.Value.IsReference)
                {
                    bool wasTemp = _ctx.HeapControl.ConsumeTemp(returnValue.Value.Value);
                    if (returnValue.Value.IsWeak)
                    {
                        returnValue = new TypedValue(_ctx.HeapControl.BuildUpgradeWeak(returnValue.Value.Value),
                            returnValue.Value.Type);
                    }
                    else
                    {
                        if (!wasTemp)
                        {
                            _ctx.HeapControl.BuildRetainStrong(returnValue.Value.Value);
                        }
                    }
                }
            }
        }

        _ctx.ReleaseScopesUpTo(null);
        if (_ctx.CurrentScope.IsDestructor)
        {
            var cleanup = _ctx.FunctionEntry.Parent.GetBasicBlock("dtorCleanup");
            _ctx.Builder.BuildBr(cleanup);

            return;
        }
        if (!returnValue.HasValue)
            _ctx.Builder.BuildRetVoid();
        else
            _ctx.Builder.BuildRet(returnValue.Value.Value);
    }
}
