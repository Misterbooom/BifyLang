#define DEBUG_COMPILE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using BifyLang.CodeGen;
using BifyLang.Parser;
using System.CommandLine;
using System.Threading.Tasks;
using BifyLang.Ast;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using Spectre.Console;

namespace BifyLang
{
    internal class Program
    {
        static Task<int> Main(string[] args)
        {
            var rawOut = new StreamWriter(Console.OpenStandardOutput()) 
            { 
                AutoFlush = true 
            };

            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Yes,
                ColorSystem = ColorSystemSupport.TrueColor, 
                Out = new AnsiConsoleOutput(rawOut)         
            });
            try
            {
                Argument<FileInfo?> inputArgument = new Argument<FileInfo?>("input") { Arity = ArgumentArity.ZeroOrOne };
                Option<FileInfo> outputOption = new Option<FileInfo>("output", aliases: ["--output", "--o"]);
                Option<bool> execOption = new Option<bool>("--exec", "--exec");
                var rootCommand = new RootCommand("BifyLang Compiler")
                {
                    inputArgument,
                    outputOption,
                    execOption
                };
                rootCommand.SetAction(result =>
                {
                    FileInfo input = result.GetValue(inputArgument);
                    if (input == null)
                    {
                        Console.WriteLine("No input file specified. Provide a file path to compile.");
                        return;
                    }

                    FileInfo output = result.GetValue(outputOption);
                    bool exec = result.GetValue(execOption);
                    CompilerFlags compilerFlags = CompilerFlags.NONE;
                    if (exec)
                        compilerFlags = compilerFlags | CompilerFlags.EXECUTE;
                    
                    string outPath = output?.FullName ?? Path.ChangeExtension(input.FullName, ".out");
                    Console.WriteLine($"Compiling {outPath}");
                   
                    RunCompiler(input.FullName, outPath, compilerFlags);
                });
                return Task.FromResult(rootCommand.Parse(args).Invoke());
            }
            catch (Exception exception)
            {
                AnsiConsole.WriteException(exception);
                return Task.FromException<int>(exception);
            }
        }

        static void RunCompiler(string filePath, string outputFilePath, CompilerFlags flags)
        {
            Console.OutputEncoding = Encoding.Unicode;
            Traceback.Instance.FilePath = filePath;
            string code = File.ReadAllText(filePath);

            Stopwatch stopwatch = new();
            stopwatch.Start();
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
            var compiler = new BifyCompiler();
            compiler.Compile((AstModule)node, filePath,outputFilePath, flags);
            //Console.Write("\x1b[38;2;255;0;0mThis is bright red\x1b[0m\n");
            //Console.Write("\x1b[38;2;0;255;0mThis is bright green\x1b[0m\n");
            //Console.Write("\x1b[38;2;0;0;255mThis is bright blue\x1b[0m\n");
            //Colorful.Console.WriteLine("This Is Red from colorful",Color.Red);
        }
        
    }
}