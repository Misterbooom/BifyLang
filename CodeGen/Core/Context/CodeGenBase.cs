using System.Diagnostics.CodeAnalysis;
using BifyLang.Ast;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Core.Context;

public abstract class CodeGenBase(CodeGenContext ctx)
{
    protected readonly CodeGenContext _ctx = ctx;

    public void Error(string msg, DiagnosticKind kind, AstNode? node = null)
    {
        _ctx.Error(msg, kind, node);
    }

    [DoesNotReturn]
    public void Fail(string msg, DiagnosticKind kind, AstNode? node = null)
    {
        _ctx.Fail(msg, kind, node);
    }

    [DoesNotReturn]
    public T Fail<T>(string msg, DiagnosticKind kind, AstNode? node = null)
    {
        return _ctx.Fail<T>(msg, kind, node);
    }

    [DoesNotReturn]
    public void AbortPoison()
    {
        _ctx.AbortPoison();
    }

    [DoesNotReturn]
    public T AbortPoison<T>()
    {
        return _ctx.AbortPoison<T>();
    }
}
