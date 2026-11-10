using System;
using BifyLang.Ast;
using BifyLang.CodeGen.Declarations;
using BifyLang.CodeGen.Statements;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
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
    public LoopContext  LoopContext = new LoopContext(null, null, null, null);
    public BifyType FunctionReturnType;
    public HeapControl HeapControl;
    public ClassInfo[] ClassesInfo {get; private set;}

    public CodeGenContext()
    {
        Context = LLVMContextRef.Create();
        Module = Context.CreateModuleWithName("main");
        Builder = Context.CreateBuilder();
        TypeRegistry = new TypeRegistry(this);
        HeapControl = new HeapControl(this);
        HeapControl.DefineRuntimeFunctions();
        BuiltinFunctions.DeclarePrintf(this);
    }

    private sealed class ScopeExitHandle(CodeGenContext ctx) : IDisposable
    {
        public void Dispose()
        {
            ctx.CurrentScope.ReleaseLocals(ctx);
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
    public void ReleaseScopesUpTo(Scope targetScope)
    {
        var scope = CurrentScope;
        while (scope != null && scope != targetScope)
        {
            scope.ReleaseLocals(this);
            scope = scope.Parent;
        }
    }
    /// <summary>
    /// Use before EnterScope 
    /// </summary>
    public IDisposable EnterLoop(LLVMBasicBlockRef breakBlock, LLVMBasicBlockRef continueBlock)
    {
        LoopContext = new LoopContext(LoopContext,breakBlock, continueBlock, CurrentScope);
        return new LoopExitHandle(this);
    }

    public LLVMModuleRef GenerateModule(AstModule module)
    {
        ClassDependencyResolver resolver = new ClassDependencyResolver();
        ClassesInfo = resolver.Resolve(module);
        BifyDebug.Log($"ClassesInfo : {resolver.ToString()}");
        new ClassLayoutBuilder(this).BuildClassesLayout(ClassesInfo);
        DeclarationDispatcher declarationDispatcher = new DeclarationDispatcher(this);
        
        declarationDispatcher.EmitModule(module);
        return Module;
    }

    public void Dispose()
    {
        Builder.Dispose();
        Module.Dispose();
        Context.Dispose();
    }
}