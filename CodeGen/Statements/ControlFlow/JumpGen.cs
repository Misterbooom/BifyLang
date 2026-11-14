#nullable enable
using System;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Core.Context;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Statements.ControlFlow;

public class JumpGen(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public void EmitBreak(AstBreak _)
    {
        if (_ctx.LoopContext.BreakBlock.Handle == IntPtr.Zero)
        {
            new BifySyntaxError("Break statement must be inside a loop or switch.").Throw();
            return;
        }

        _ctx.ReleaseScopesUpTo(_ctx.LoopContext.Scope);
        _ctx.Builder.BranchIfUnterminated(_ctx.LoopContext.BreakBlock);
    }

    public void EmitContinue(AstContinue _)
    {
        if (_ctx.LoopContext.ContinueBlock.Handle == IntPtr.Zero)
        {
            new BifySyntaxError("Continue statement must be inside a loop.").Throw();
            return;
        }

        var targetLoop = _ctx.LoopContext;
        while (targetLoop.Parent != null && targetLoop.Parent.ContinueBlock.Handle == targetLoop.ContinueBlock.Handle)
        {
            targetLoop = targetLoop.Parent;
        }

        _ctx.ReleaseScopesUpTo(targetLoop.Scope);
        _ctx.Builder.BranchIfUnterminated(targetLoop.ContinueBlock);
    }
}
