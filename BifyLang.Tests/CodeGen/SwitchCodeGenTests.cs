#nullable enable
using BifyLang.Tests.Infrastructure;
using NUnit.Framework;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class SwitchCodeGenTests : BifyTestBase
{
    [Test]
    public void Switch_Basic()
    {
        AssertValidFunction("void test(int x) { int y = 0; switch (x) { case 1: { y = 10; } default: { y = 20; } } }");
    }

    [Test]
    public void Switch_MultipleCases()
    {
        AssertValidFunction("void test(int x) { int y = 0; switch (x) { case 1: { y = 10; } case 2: { y = 20; } case 3: { y = 30; } default: { y = 40; } } }");
    }

    [Test]
    public void Switch_DefaultCase()
    {
        AssertValidFunction("void test(int x) { int y = 0; switch (x) { case 5: { y = 50; } default: { y = -1; } } }");
    }

    [Test]
    public void Switch_WithoutDefault()
    {
        AssertValidFunction("void test(int x) { int y = 0; switch (x) { case 1: { y = 10; } case 2: { y = 20; } } }");
    }

    [Test]
    public void Switch_WithBreak()
    {
        AssertValidFunction("void test(int x) { int y = 0; switch (x) { case 1: { y = 10; break; } default: { y = 20; break; } } }");
    }

    [Test]
    public void Switch_AllBranchesReturn()
    {
        AssertValidFunction("int test(int x) { switch (x) { case 1: { return 10; } case 2: { return 20; } default: { return 30; } } }");
    }

    [Test]
    public void Switch_InsideWhileLoop()
    {
        AssertValidFunction("void test() { int i = 0; while (i < 10) { switch (i) { case 5: { break; } } i++; } }");
    }

    [TestCase("char c", "case 65: { val = 1; }", TestName = "Switch_CharExpression")]
    [TestCase("float x", "case 1.5: { val = 1; } case 2.5: { val = 2; }", TestName = "Switch_FloatExpression")]
    [TestCase("double d", "case 3.14: { val = 314; }", TestName = "Switch_DoubleExpression")]
    public void Switch_TypeExpressions(string param, string cases)
    {
        AssertValidFunction($"void test({param}) {{ int val = 0; switch ({(param.Split(' ')[1])}) {{ {cases} default: {{ val = 0; }} }} }}");
    }

    [Test]
    public void Switch_FloatWithBreak()
    {
        AssertValidFunction("void test(float x) { int y = 0; switch (x) { case 1.0: { y = 1; break; } default: { y = 2; break; } } }");
    }

    [Test]
    public void Switch_FloatAllBranchesReturn()
    {
        AssertValidFunction("int test(float x) { switch (x) { case 1.0: { return 1; } case 2.0: { return 2; } default: { return 3; } } }");
    }

    [Test]
    public void Switch_OnInt_EmitsLlvmSwitchInstruction()
    {
        using var m = Bify.Compile("int test(int x) { switch (x) { case 1: { return 10; } default: { return 0; } } }");
        m.AssertValid();
        Assert.That(m.IR, Does.Contain("switch i32"));
    }

    [Test]
    public void Switch_OnFloat_LowersToCompareChain()
    {
        using var m = Bify.Compile("int test(float x) { switch (x) { case 1.0: { return 10; } default: { return 0; } } }");
        m.AssertValid();
        Assert.That(m.IR, Does.Not.Contain("switch float"));
    }
}
