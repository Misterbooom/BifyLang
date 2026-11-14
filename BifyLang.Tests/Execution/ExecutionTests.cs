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
        int main() {
            printf("Hello %s\n", "Bify");
            return 0;
        }
        """, "Hello Bify");
    }

    [Test]
    public void E2E_ArithmeticAndVariables()
    {
        AssertExecution("""
        int main() {
            int a = 10;
            int b = 25;
            int sum = a + b;
            printf("%d\n", sum);
            return 0;
        }
        """, "35");
    }

    [Test]
    public void E2E_ForLoopSum()
    {
        AssertExecution("""
        int main() {
            int sum = 0;
            for (int i = 0; i < 100; i++) {
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
        int pascal(int row, int col) {
            int result = 1;
            int i = 1;
            while (i <= col) {
                int num = (row - i) + 1;
                result = (result * num) / i;
                i++;
            }
            return result;
        }

        int main() {
            int p0 = pascal(4, 0);
            int p2 = pascal(4, 2);
            int p4 = pascal(4, 4);
            printf("%d %d %d\n", p0, p2, p4);
            return 0;
        }
        """, "1 6 1");
    }

    [Test]
    public void E2E_SwitchBranching()
    {
        AssertExecution("""
        int evaluate(int x) {
            switch (x) {
                case 1: { return 100; }
                case 2: { return 200; }
                default: { return 999; }
            }
        }

        int main() {
            int r1 = evaluate(1);
            int r2 = evaluate(2);
            int r3 = evaluate(5);
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
            public int multiplier;
            public constructor(int m) {
                this.multiplier = m;
            }
            public int multiply(int x) {
                return x * this.multiplier;
            }
        }

        int main() {
            Calculator calc = new Calculator(7);
            int res = calc.multiply(6);
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
            public virtual int speak() {
                return 1;
            }
        }

        class Dog : Animal {
            public override int speak() {
                return 2;
            }
        }

        int main() {
            Animal a = new Dog();
            int sound = a.speak();
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
            public int value;
            public constructor(int v) {
                this.value = v;
            }
            public int getValue() {
                return this.value;
            }
        }

        int main() {
            Node strong = new Node(99);
            weak Node w = strong;
            Node recovered = w;
            printf("%d %d\n", recovered.value, w.getValue());
            return 0;
        }
        """, "99 99");
    }
}
