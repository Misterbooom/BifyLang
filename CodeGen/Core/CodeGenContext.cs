using System;
using BifyLang.Ast;
using BifyLang.CodeGen.Declarations;
using BifyLang.CodeGen.Statements;
using BifyLang.CodeGen.Types;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public class CodeGenContext : IDisposable
{
    public LLVMContextRef Context;
    public LLVMBasicBlockRef FunctionEntry;

    public LLVMModuleRef Module { get; private set; }
    public LLVMBuilderRef Builder { get; private set; }
    public Scope CurrentScope { get; private set; } = new Scope(parent: null);
    public TypeRegistry TypeRegistry { get; private set; }
    public LoopContext  LoopContext = new LoopContext(null, null, null);
    public BifyType FunctionReturnType;

    public CodeGenContext()
    {
        Context = LLVMContextRef.Create();
        Module = Context.CreateModuleWithName("main");
        Builder = Context.CreateBuilder();
        TypeRegistry = new TypeRegistry(this);
        BuiltinFunctions.DeclarePrintf(this);
    }

    private sealed class ScopeExitHandle(CodeGenContext ctx) : IDisposable
    {
        public void Dispose()
        {
            ctx.CurrentScope = ctx.CurrentScope.Parent
                               ?? throw new InvalidOperationException("Cannot exit root scope");
        }
    }
    private sealed class LoopExitHandle(CodeGenContext ctx) : IDisposable
    {
        public void Dispose()
        {
            ctx.LoopContext = ctx.LoopContext.Parent;
        }
    }
    public IDisposable EnterScope()
    {
        CurrentScope = new Scope(parent: CurrentScope);
        return new ScopeExitHandle(this);
    }

    public IDisposable EnterLoop(LLVMBasicBlockRef breakBlock, LLVMBasicBlockRef continueBlock)
    {
        LoopContext = new LoopContext(LoopContext,breakBlock, continueBlock);
        return new LoopExitHandle(this);
    }

    public LLVMModuleRef GenerateModule(AstModule module)
    {
        DeclarationDispatcher declarationDispatcher = new DeclarationDispatcher(this);
        declarationDispatcher.EmitModule(module);
        return Module;
    }

    public void Dispose()
    {
        Module.Dispose();
        Builder.Dispose();
        Context.Dispose();
    }
}