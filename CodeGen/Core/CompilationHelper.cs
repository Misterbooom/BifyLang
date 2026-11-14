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
    public static AstModule ParseToAst(string filePath)
    {
        Traceback.Instance.FilePath = filePath;
        string code = File.ReadAllText(filePath);

        Stopwatch stopwatch = Stopwatch.StartNew();
        MyLexer lexer = new(code);
        List<Token> tokens = lexer.Tokenize();
        tokens.WriteTokens();
        stopwatch.Stop();
        Console.WriteLine($"Tokenization completed in {stopwatch.ElapsedMilliseconds} ms.");

        string[] codeByLine = code.Split('\n');
        stopwatch.Restart();
        AstTree astParser = new(codeByLine);
        AstNode node = astParser.ParseTokens(tokens);
        stopwatch.Stop();
        Console.WriteLine($"AST parsing completed in {stopwatch.ElapsedMilliseconds} ms.");

        BifyDebug.Log(node.ToString());

        return (AstModule)node;
    }

    public static void CompileFile(string filePath, string? outputFilePath = null, CompilerFlags flags = CompilerFlags.NONE)
    {
        string resolvedOutPath = outputFilePath ?? Path.ChangeExtension(filePath, ".out");
        Console.WriteLine($"Compiling {resolvedOutPath}");

        AstModule astModule = ParseToAst(filePath);

        var compiler = new BifyCompiler();
        compiler.Compile(astModule, filePath, resolvedOutPath, flags);
    }
}