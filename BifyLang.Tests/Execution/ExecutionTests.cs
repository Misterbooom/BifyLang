#nullable enable
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.Execution;

[TestFixture]
[Category("E2E")]
public class ExecutionTests : BifyTestBase
{
    [Test]
    public void E2E_HelloWorldPrintf()
    {
        AssertExecution("""
        func main() -> int {
            printf("Hello %s\n", "Bify");
            return 0;
        }
        """, "Hello Bify");
    }

    [Test]
    public void E2E_ArithmeticAndVariables()
    {
        AssertExecution("""
        func main() -> int {
            val a: int = 10;
            val b: int = 25;
            val sum: int = a + b;
            printf("%d\n", sum);
            return 0;
        }
        """, "35");
    }

    [Test]
    public void E2E_ForLoopSum()
    {
        AssertExecution("""
        func main() -> int {
            mut sum: int = 0;
            for (mut i: int = 0; i < 100; i++) {
                sum += i;
            }
            printf("%d\n", sum);
            return 0;
        }
        """, "4950");
    }

    [Test]
    public void E2E_PascalCoefficients()
    {
        AssertExecution("""
        func pascal(row: int, col: int) -> int {
            mut result: int = 1;
            mut i: int = 1;
            while (i <= col) {
                val num: int = (row - i) + 1;
                result = (result * num) / i;
                i++;
            }
            return result;
        }

        func main() -> int {
            val p0: int = pascal(4, 0);
            val p2: int = pascal(4, 2);
            val p4: int = pascal(4, 4);
            printf("%d %d %d\n", p0, p2, p4);
            return 0;
        }
        """, "1 6 1");
    }

    [Test]
    public void E2E_SwitchBranching()
    {
        AssertExecution("""
        func evaluate(x: int) -> int {
            switch (x) {
                case 1: { return 100; }
                case 2: { return 200; }
                default: { return 999; }
            }
        }

        func main() -> int {
            val r1: int = evaluate(1);
            val r2: int = evaluate(2);
            val r3: int = evaluate(5);
            printf("%d %d %d\n", r1, r2, r3);
            return 0;
        }
        """, "100 200 999");
    }

    [Test]
    public void E2E_ClassInstantiationAndMethodCall()
    {
        AssertExecution("""
        class Calculator {
            public mut multiplier: int;
            public constructor(m: int) {
                this.multiplier = m;
            }
            public func multiply(x: int) -> int {
                return x * this.multiplier;
            }
        }

        func main() -> int {
            val calc: Calculator = new Calculator(7);
            val res: int = calc.multiply(6);
            printf("%d\n", res);
            return 0;
        }
        """, "42");
    }

    [Test]
    public void E2E_PolymorphicDispatchVirtualOverride()
    {
        AssertExecution("""
        class Animal {
            public virtual func speak() -> int {
                return 1;
            }
        }

        class Dog : Animal {
            public override func speak() -> int {
                return 2;
            }
        }

        func main() -> int {
            val a: Animal = new Dog();
            val sound: int = a.speak();
            printf("%d\n", sound);
            return 0;
        }
        """, "2");
    }

    [Test]
    public void E2E_WeakReferenceUpgradeAndAccess()
    {
        AssertExecution("""
        class Node {
            public mut value: int;
            public constructor(v: int) {
                this.value = v;
            }
            public func getValue() -> int {
                return this.value;
            }
        }

        func main() -> int {
            val strong: Node = new Node(99);
            weak val w: Node = strong;
            val recovered: Node = w;
            printf("%d %d\n", recovered.value, w.getValue());
            return 0;
        }
        """, "99 99");
    }
}
