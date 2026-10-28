#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;
using NUnit.Framework;

namespace BifyLang.Tests.Semantics;

[TestFixture]
public class FunctionPathCheckerTests : BifyTestBase
{
    private static void ParseAndCheck(string code)
    {
        var module = Bify.Parse(code);
        var ctx = new CodeGenContext();
        foreach (var child in module.ChildNodes)
        {
            if (child is AstFunctionDecl fn)
            {
                _ = new FunctionPathChecker(fn, ctx);
            }
            else if (child is AstClass cls && cls.BodyNode is AstBlock block)
            {
                foreach (var member in block.ChildNodes)
                {
                    if (member is AstFunctionDecl method)
                        _ = new FunctionPathChecker(method, ctx);
                }
            }
        }
    }

    private static void AssertSuccess(string code)
    {
        ParseAndCheck(code);
        var err = Traceback.Instance.GetError();
        Assert.That(err, Is.Null, $"Expected no error, but got {err?.GetType().Name}: {err?.Message}");
    }

    private static void AssertTypeError(string code, string? expectedSubstring = null)
    {
        BifyAssert.ActionThrows<BifyTypeError>(() => ParseAndCheck(code), expectedSubstring);
    }

    [Test]
    public void PathCheck_SimpleReturn()
    {
        AssertSuccess("int getVal() { return 42; }");
    }

    [Test]
    public void PathCheck_IfElseBothReturn()
    {
        AssertSuccess("int max(int a, int b) { if (a > b) { return a; } else { return b; } }");
    }

    [Test]
    public void PathCheck_IfElseIfElseAllReturn()
    {
        AssertSuccess("int sign(int x) { if (x > 0) { return 1; } else if (x < 0) { return -1; } else { return 0; } }");
    }

    [Test]
    public void PathCheck_VoidNoReturn()
    {
        AssertSuccess("void doWork() { int a = 1; int b = 2; }");
    }

    [Test]
    public void PathCheck_VoidEmptyReturn()
    {
        AssertSuccess("void doWork(int a) { if (a > 0) { return; } int b = 2; }");
    }

    [Test]
    public void PathCheck_EarlyReturnDeadCodePruned()
    {
        AssertSuccess("int compute() { return 100; int dead = 50; }");
    }

    [Test]
    public void PathCheck_InfiniteWhileReturns()
    {
        AssertSuccess("int loopForever() { while (true) { return 1; } }");
    }

    [Test]
    public void PathCheck_ConstructorVoidAllowed()
    {
        AssertSuccess("class TestClass { int x; constructor(int val) { this.x = val; return; } }");
    }

    [Test]
    public void PathCheck_SwitchAllReturn()
    {
        AssertSuccess("int getVal(int x) { switch (x) { case 1: return 10; case 2: return 20; default: return 0; } }");
    }

    [TestCase("int missing() { int a = 5; }", "Not all execution paths return a value for int", TestName = "PathCheck_MissingReturnNonVoid")]
    [TestCase("int check(int a) { if (a > 0) { return a; } }", "Not all execution paths return a value for int", TestName = "PathCheck_IfWithoutElseFails")]
    [TestCase("int check(int a) { if (a > 0) { int x = 1; } else { return 2; } }", "Not all execution paths return a value for int", TestName = "PathCheck_IfElseBranchMissingReturn")]
    [TestCase("int getValue() { return; }", "Expected int but got void", TestName = "PathCheck_EmptyReturnInNonVoid")]
    [TestCase("int loop() { while (true) { break; } }", "Not all execution paths return a value for int", TestName = "PathCheck_WhileTrueWithBreakDoesNotGuaranteeReturn")]
    [TestCase("int getVal(int x) { switch (x) { case 1: return 10; } }", "Not all execution paths return a value for int", TestName = "PathCheck_SwitchMissingDefaultFails")]
    public void PathCheck_FailureCases(string code, string expectedSubstring)
    {
        AssertTypeError(code, expectedSubstring);
    }
}
