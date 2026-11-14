#nullable enable
using BifyLang.Exceptions;

namespace BifyLang.Tests.Infrastructure;

[NonParallelizable]
public abstract class BifyTestBase
{
    [SetUp]
    public virtual void SetUp()
    {
    }

    [TearDown]
    public virtual void TearDown()
    {
    }

    protected static void AssertValidFunction(string code, string funcName = "test")
    {
        using var m = Bify.Compile(code);
        m.AssertValid();
        m.Function(funcName);
    }

    protected static Diagnostic AssertCompileError(string code, DiagnosticKind expectedKind, string? expectedSubstring = null)
        => BifyAssert.CompileError(code, expectedKind, expectedSubstring);

    protected static void AssertExecution(string code, string expectedStdout, int expectedExitCode = 0)
    {
        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(expectedExitCode));
        Assert.That(result.Stdout.Trim(), Is.EqualTo(expectedStdout.Trim()));
    }

    protected static ExpressionTestContext CreateExprContext(string funcName = "testExpr")
        => new(funcName);
}
