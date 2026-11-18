#nullable enable
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class SwitchCodeGenTests : BifyTestBase
{
    [Test]
    public void Switch_Basic()
    {
        AssertValidFunction("func test(x: int) { mut y: int = 0; switch (x) { case 1: { y = 10; } default: { y = 20; } } }");
    }

    [Test]
    public void Switch_MultipleCases()
    {
        AssertValidFunction("func test(x: int) { mut y: int = 0; switch (x) { case 1: { y = 10; } case 2: { y = 20; } case 3: { y = 30; } default: { y = 40; } } }");
    }

    [Test]
    public void Switch_DefaultCase()
    {
        AssertValidFunction("func test(x: int) { mut y: int = 0; switch (x) { case 5: { y = 50; } default: { y = -1; } } }");
    }

    [Test]
    public void Switch_WithoutDefault()
    {
        AssertValidFunction("func test(x: int) { mut y: int = 0; switch (x) { case 1: { y = 10; } case 2: { y = 20; } } }");
    }

    [Test]
    public void Switch_WithBreak()
    {
        AssertValidFunction("func test(x: int) { mut y: int = 0; switch (x) { case 1: { y = 10; break; } default: { y = 20; break; } } }");
    }

    [Test]
    public void Switch_AllBranchesReturn()
    {
        AssertValidFunction("func test(x: int) -> int { switch (x) { case 1: { return 10; } case 2: { return 20; } default: { return 30; } } }");
    }

    [Test]
    public void Switch_InsideWhileLoop()
    {
        AssertValidFunction("func test() { mut i: int = 0; while (i < 10) { switch (i) { case 5: { break; } } i++; } }");
    }

    [TestCase("c: char", "case 65: { res = 1; }", TestName = "Switch_CharExpression")]
    [TestCase("x: float", "case 1.5: { res = 1; } case 2.5: { res = 2; }", TestName = "Switch_FloatExpression")]
    [TestCase("d: double", "case 3.14: { res = 314; }", TestName = "Switch_DoubleExpression")]
    public void Switch_TypeExpressions(string param, string cases)
    {
        AssertValidFunction($"func test({param}) {{ mut res: int = 0; switch ({param.Split(':')[0]}) {{ {cases} default: {{ res = 0; }} }} }}");
    }

    [Test]
    public void Switch_FloatWithBreak()
    {
        AssertValidFunction("func test(x: float) { mut y: int = 0; switch (x) { case 1.0: { y = 1; break; } default: { y = 2; break; } } }");
    }

    [Test]
    public void Switch_FloatAllBranchesReturn()
    {
        AssertValidFunction("func test(x: float) -> int { switch (x) { case 1.0: { return 1; } case 2.0: { return 2; } default: { return 3; } } }");
    }

    [Test]
    public void Switch_OnInt_EmitsLlvmSwitchInstruction()
    {
        using var m = Bify.Compile("func test(x: int) -> int { switch (x) { case 1: { return 10; } default: { return 0; } } }");
        m.AssertValid();
        Assert.That(m.IR, Does.Contain("switch i32"));
    }

    [Test]
    public void Switch_OnFloat_LowersToCompareChain()
    {
        using var m = Bify.Compile("func test(x: float) -> int { switch (x) { case 1.0: { return 10; } default: { return 0; } } }");
        m.AssertValid();
        Assert.That(m.IR, Does.Not.Contain("switch float"));
    }

    [Test]
    public void Switch_Fallback_EvaluatesSubjectOnce()
    {
        const string code = """
            func getNum() -> float { return 1.0; }
            func test() -> int {
                switch (getNum()) {
                    case 1.0: { return 1; }
                    case 2.0: { return 2; }
                    case 3.0: { return 3; }
                    default: { return 0; }
                }
            }
            """;
        using var m = Bify.Compile(code);
        m.AssertValid();

        int callCount = (m.IR.Split("call float @main.getNum(").Length - 1);
        Assert.That(callCount, Is.EqualTo(1));
    }

    [Test]
    public void Switch_String_UsesStrcmpInsteadOfPointerEquality()
    {
        const string code = """
            func pick(s: string) -> int {
                switch (s) {
                    case "alpha": { return 1; }
                    case "beta": { return 2; }
                    default: { return 0; }
                }
            }
            """;
        using var m = Bify.Compile(code);
        m.AssertValid();
        Assert.That(m.IR, Does.Contain("call i32 @strcmp("));
    }
}
