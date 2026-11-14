#define DEBUG_COMPILE
using System;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BifyLang.CodeGen;
using BifyLang.CodeGen.Core;
using Spectre.Console;

namespace BifyLang;

internal class Program
{
    static Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.Unicode;

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


        var inputArgument = new Argument<FileInfo?>("input") { Arity = ArgumentArity.ZeroOrOne };
        var outputOption = new Option<FileInfo?>("output", aliases: ["--output", "-o"]);
        var execOption = new Option<bool>("exec", aliases: ["--exec", "-e"]);

        var rootCommand = new RootCommand("BifyLang Compiler")
        {
            inputArgument,
            outputOption,
            execOption
        };

        rootCommand.SetAction(result =>
        {
            FileInfo? input = result.GetValue(inputArgument);
            if (input == null)
            {
                Console.WriteLine("No input file specified. Provide a file path to compile.");
                return;
            }

            FileInfo? output = result.GetValue(outputOption);
            bool exec = result.GetValue(execOption);

            var compilerFlags = exec ? CompilerFlags.EXECUTE : CompilerFlags.NONE;

            CompilationHelper.CompileFile(input.FullName, output?.FullName, compilerFlags);
        });

        return Task.FromResult(rootCommand.Parse(args).Invoke());
    }
}
