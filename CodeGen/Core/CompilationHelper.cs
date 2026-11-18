using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BifyLang.Ast;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using BifyLang.Parser;

namespace BifyLang.CodeGen.Core;

public static class CompilationHelper
{
    public static AstModule ParseToAst(string filePath, DiagnosticBag? diagnostics = null)
    {
        DiagnosticBag bag = diagnostics ?? new DiagnosticBag();
        BifyDebug.SetLogFile("bify.log");
        string code = File.ReadAllText(filePath);

        Stopwatch stopwatch = Stopwatch.StartNew();
        MyLexer lexer = new(code, bag, filePath);
        List<Token> tokens = lexer.Tokenize();
        stopwatch.Stop();
        BifyDebug.Log($"Tokenization completed in {stopwatch.ElapsedMilliseconds} ms.{Environment.NewLine}{tokens.TokensToString()}");

        string[] codeByLine = code.Split('\n');
        stopwatch.Restart();
        AstTree astParser = new(codeByLine, bag, filePath);
        AstNode node = astParser.ParseTokens(tokens);
        stopwatch.Stop();
        BifyDebug.Log($"AST parsing completed in {stopwatch.ElapsedMilliseconds} ms.{Environment.NewLine}{node}");

        return (AstModule)node;
    }

    public static void CompileFile(string filePath, string? outputFilePath = null, CompilerFlags flags = CompilerFlags.NONE)
    {
        string resolvedOutPath = outputFilePath ?? Path.ChangeExtension(filePath, ".out");
        Console.WriteLine($"Compiling {resolvedOutPath}");

        DiagnosticBag diagnostics = new();
        AstModule astModule = ParseToAst(filePath, diagnostics);

        var compiler = new BifyCompiler();
        compiler.Compile(astModule, filePath, resolvedOutPath, flags, diagnostics);
    }
}