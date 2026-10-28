#nullable enable
using System;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Expressions;
using LLVMSharp.Interop;

namespace BifyLang.Tests.Infrastructure;

public sealed class ExpressionTestContext : IDisposable
{
    public CodeGenContext Ctx { get; }
    public ExpressionDispatcher Dispatcher { get; }
    public LLVMValueRef Function { get; }

    public ExpressionTestContext(string funcName = "testExpr")
    {
        Ctx = new CodeGenContext();
        var funcType = LLVMTypeRef.CreateFunction(Ctx.Context.VoidType, [], false);
        Function = Ctx.Module.AddFunction(funcName, funcType);
        var entry = Function.AppendBasicBlock("entry");
        Ctx.Builder.PositionAtEnd(entry);
        Dispatcher = new ExpressionDispatcher(Ctx);
    }

    public void Dispose() => Ctx.Dispose();
}
