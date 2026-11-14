#nullable enable
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class ControlFlowCodeGenTests : BifyTestBase
{
    [Test]
    public void ControlFlow_IfSimple()
    {
        AssertValidFunction("void test(int x) { int y = 0; if (x > 0) { y = 1; } }");
    }

    [Test]
    public void ControlFlow_IfElse()
    {
        AssertValidFunction("void test(int x) { int y = 0; if (x > 0) { y = 1; } else { y = 2; } }");
    }

    [Test]
    public void ControlFlow_IfElseIfElse()
    {
        AssertValidFunction("""
        void test(int x) {
            int y = 0;
            if (x > 10) { y = 1; }
            else if (x > 5) { y = 2; }
            else if (x > 0) { y = 3; }
            else { y = 4; }
        }
        """);
    }

    [Test]
    public void ControlFlow_IfBothBranchesReturn()
    {
        AssertValidFunction("int test(int x) { if (x > 0) { return 1; } else { return 2; } }");
    }

    [Test]
    public void ControlFlow_IfElseIfAllBranchesReturn()
    {
        AssertValidFunction("int test(int x) { if (x > 10) { return 1; } else if (x > 5) { return 2; } else { return 3; } }");
    }

    [Test]
    public void ControlFlow_WhileSimple()
    {
        AssertValidFunction("void test() { int i = 0; while (i < 10) { i++; } }");
    }

    [Test]
    public void ControlFlow_WhileWithConditionAndReturn()
    {
        AssertValidFunction("""
        int test(int n) {
            int sum = 0;
            int i = 1;
            while (i <= n) {
                sum += i;
                i++;
            }
            return sum;
        }
        """);
    }

    [Test]
    public void ControlFlow_ForSimple()
    {
        AssertValidFunction("void test() { for (int i = 0; i < 10; i++) {} }");
    }

    [Test]
    public void ControlFlow_ForSum()
    {
        AssertValidFunction("int test(int n) { int sum = 0; for (int i = 1; i <= n; i++) { sum += i; } return sum; }");
    }

    [Test]
    public void ControlFlow_ForTwoConsecutiveLoops()
    {
        AssertValidFunction("void test() { for (int i = 0; i < 5; i++) {} for (int i = 0; i < 5; i++) {} }");
    }

    [Test]
    public void ControlFlow_ForEarlyReturn()
    {
        AssertValidFunction("int test() { for (int i = 0; i < 10; i++) { return i; } return 0; }");
    }

    [Test]
    public void ControlFlow_ForBreak()
    {
        AssertValidFunction("void test() { for (int i = 0; i < 10; i++) { break; } }");
    }

    [Test]
    public void ControlFlow_ForContinue()
    {
        AssertValidFunction("void test() { for (int i = 0; i < 10; i++) { continue; } }");
    }
}
