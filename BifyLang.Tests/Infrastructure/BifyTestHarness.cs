#nullable enable
using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen;
using BifyLang.CodeGen.Core;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;
using NUnit.Framework;

namespace BifyLang.Tests.Infrastructure;

public static class Bify
{
    public static List<Token> Lex(string code)
    {
        var lexer = new MyLexer(code);
        return lexer.Tokenize();
    }

    public static AstModule Parse(string code)
    {
        string[] lines = code.Split('\n');
        Traceback.Instance.InitializeSource(lines);
        var tokens = Lex(code);
        var tree = new AstTree(lines);
        return (AstModule)tree.ParseTokens(tokens);
    }

    public static CompiledModule Compile(string code)
    {
        var moduleNode = Parse(code);
        var ctx = new CodeGenContext();
        var module = ctx.GenerateModule(moduleNode);
        return new CompiledModule(module, ctx);
    }
}

public sealed class CompiledModule : IDisposable
{
    public LLVMModuleRef Module { get; }
    public CodeGenContext Context { get; }

    public string IR => Module.ToString();

    public CompiledModule(LLVMModuleRef module, CodeGenContext context)
    {
        Module = module;
        Context = context;
    }

    public void AssertValid()
    {
        if (!BifyCompiler.VerifyModule(Module, out var error))
        {
            Assert.Fail($"LLVM Module verification failed:\n{error}\n\nGenerated IR:\n{IR}");
        }
    }

    public LLVMValueRef Function(string name)
    {
        var fn = Module.GetNamedFunction(name);
        Assert.That(fn.Handle, Is.Not.EqualTo(IntPtr.Zero), $"Expected function '{name}' to exist in compiled module.\nIR:\n{IR}");
        return fn;
    }

    public void Dispose()
    {
        Context.Dispose();
    }
}
