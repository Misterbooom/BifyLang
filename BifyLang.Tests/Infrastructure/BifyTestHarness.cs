#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen;
using BifyLang.CodeGen.Core.Context;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.Tests.Infrastructure;

public static class Bify
{
    public static List<Token> Lex(string code, DiagnosticBag? diagnostics = null, string filePath = "main")
    {
        var lexer = new MyLexer(code, diagnostics, filePath);
        return lexer.Tokenize();
    }

    public static AstModule Parse(string code, DiagnosticBag? diagnostics = null, string filePath = "main")
    {
        DiagnosticBag bag = diagnostics ?? new DiagnosticBag();
        string[] lines = code.Split('\n');
        var tokens = Lex(code, bag, filePath);
        var tree = new AstTree(lines, bag, filePath);
        return tree.ParseTokens(tokens);
    }

    public static CompiledModule Compile(string code, DiagnosticBag? diagnostics = null, string filePath = "main")
    {
        DiagnosticBag bag = diagnostics ?? new DiagnosticBag();
        var moduleNode = Parse(code, bag, filePath);
        var ctx = new CodeGenContext(bag);
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
        if (Context.Diagnostics.HasErrors)
        {
            string messages = string.Join(
                Environment.NewLine,
                Context.Diagnostics.Diagnostics.Select(d => $"{d.Kind}: {d.Message}")
            );
            Assert.Fail($"Unexpected compiler diagnostics:\n{messages}");
        }
        if (!BifyCompiler.VerifyModule(Module, out var error))
        {
            Assert.Fail($"LLVM Module verification failed:\n{error}\n\nGenerated IR:\n{IR}");
        }
    }

    public LLVMValueRef Function(string name)
    {
        var fn = Module.GetNamedFunction(name);
        if (fn.Handle == IntPtr.Zero && !name.Contains('.'))
            fn = Module.GetNamedFunction(BifyLang.CodeGen.Core.NameMangler.GenerateFunctionName("main", name));
        Assert.That(fn.Handle, Is.Not.EqualTo(IntPtr.Zero), $"Expected function '{name}' to exist in compiled module.\nIR:\n{IR}");
        return fn;
    }

    public void Dispose()
    {
        Context.Dispose();
    }
}
