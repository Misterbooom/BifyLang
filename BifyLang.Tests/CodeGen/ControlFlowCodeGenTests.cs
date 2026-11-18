#nullable enable
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class ControlFlowCodeGenTests : BifyTestBase
{
    [Test]
    public void ControlFlow_IfSimple()
    {
        AssertValidFunction("func test(x: int) { mut y: int = 0; if (x > 0) { y = 1; } }");
    }

    [Test]
    public void ControlFlow_IfElse()
    {
        AssertValidFunction("func test(x: int) { mut y: int = 0; if (x > 0) { y = 1; } else { y = 2; } }");
    }

    [Test]
    public void ControlFlow_IfElseIfElse()
    {
        AssertValidFunction("""
        func test(x: int) {
            mut y: int = 0;
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
        AssertValidFunction("func test(x: int) -> int { if (x > 0) { return 1; } else { return 2; } }");
    }

    [Test]
    public void ControlFlow_IfElseIfAllBranchesReturn()
    {
        AssertValidFunction("func test(x: int) -> int { if (x > 10) { return 1; } else if (x > 5) { return 2; } else { return 3; } }");
    }

    [Test]
    public void ControlFlow_WhileSimple()
    {
        AssertValidFunction("func test() { mut i: int = 0; while (i < 10) { i++; } }");
    }

    [Test]
    public void ControlFlow_WhileWithConditionAndReturn()
    {
        AssertValidFunction("""
        func test(n: int) -> int {
            mut sum: int = 0;
            mut i: int = 1;
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
        AssertValidFunction("func test() { for (mut i: int = 0; i < 10; i++) {} }");
    }

    [Test]
    public void ControlFlow_ForSum()
    {
        AssertValidFunction("func test(n: int) -> int { mut sum: int = 0; for (mut i: int = 1; i <= n; i++) { sum += i; } return sum; }");
    }

    [Test]
    public void ControlFlow_ForTwoConsecutiveLoops()
    {
        AssertValidFunction("func test() { for (mut i: int = 0; i < 5; i++) {} for (mut i: int = 0; i < 5; i++) {} }");
    }

    [Test]
    public void ControlFlow_ForEarlyReturn()
    {
        AssertValidFunction("func test() -> int { for (mut i: int = 0; i < 10; i++) { return i; } return 0; }");
    }

    [Test]
    public void ControlFlow_ForBreak()
    {
        AssertValidFunction("func test() { for (mut i: int = 0; i < 10; i++) { break; } }");
    }

    [Test]
    public void ControlFlow_ForContinue()
    {
        AssertValidFunction("func test() { for (mut i: int = 0; i < 10; i++) { continue; } }");
    }
}
