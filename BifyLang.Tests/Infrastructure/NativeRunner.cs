#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using BifyLang.CodeGen;
using NUnit.Framework;

namespace BifyLang.Tests.Infrastructure;

public record ExecutionResult(int ExitCode, string Stdout, string Stderr);

public static class NativeRunner
{
    public static ExecutionResult Run(string code, int timeoutMs = 5000)
    {
        string tempExe = Path.Combine(Path.GetTempPath(), $"bify_test_{Guid.NewGuid():N}.out");
        try
        {
            var compiler = new BifyCompiler();
            using var compiled = Bify.Compile(code);
            compiled.AssertValid();

            compiler.GenerateExecutable(tempExe, CompilerFlags.NONE, compiled.Module);

            if (!File.Exists(tempExe))
            {
                Assert.Fail($"Compiler did not produce output executable at {tempExe}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = tempExe,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { }
                Assert.Fail($"Process timed out after {timeoutMs}ms.\nStdout so far: {stdout}\nStderr: {stderr}");
            }

            return new ExecutionResult(process.ExitCode, stdout, stderr);
        }
        finally
        {
            if (File.Exists(tempExe))
            {
                try { File.Delete(tempExe); } catch { }
            }
        }
    }
}
