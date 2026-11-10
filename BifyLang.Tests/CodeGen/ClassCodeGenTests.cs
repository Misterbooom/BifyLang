#nullable enable
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;
using NUnit.Framework;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class ClassCodeGenTests : BifyTestBase
{
    [Test]
    public void Class_EmptyClassAndInstantiation()
    {
        string code = """
        class Dummy {
        }
        void test() {
            Dummy d = new Dummy();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_AttributeDefaultValuesAndRead()
    {
        string code = """
        class Point {
            public int x = 10;
            public int y = 20;
        }
        void test() {
            Point p = new Point();
            int a = p.x;
            int b = p.y;
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_ConstructorInitializesAttributes()
    {
        string code = """
        class Point {
            public int x;
            public int y;
            public constructor(int a, int b) {
                this.x = a;
                this.y = b;
            }
        }
        void test() {
            Point p = new Point(5, 15);
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_MethodCallAndReturn()
    {
        string code = """
        class Calculator {
            public int add(int a, int b) {
                return a + b;
            }
        }
        void test() {
            Calculator c = new Calculator();
            int res = c.add(3, 4);
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_MethodAccessAndMutateThis()
    {
        string code = """
        class Counter {
            public int count = 0;
            public void increment() {
                this.count += 1;
            }
            public int getCount() {
                return this.count;
            }
        }
        void test() {
            Counter c = new Counter();
            c.increment();
            int res = c.getCount();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_AttributeAssignmentFromOutside()
    {
        string code = """
        class Box {
            public int val = 0;
        }
        void test() {
            Box b = new Box();
            b.val = 42;
            b.val += 8;
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_PrivateAttributeThrowsOutside()
    {
        string code = """
        class Secret {
            private int val = 100;
        }
        void test() {
            Secret s = new Secret();
            int x = s.val;
        }
        """;
        BifyAssert.CompileError<BifyAttributeError>(code);
    }

    [Test]
    public void Class_PrivateMethodThrowsOutside()
    {
        string code = """
        class Secret {
            private int get() {
                return 100;
            }
        }
        void test() {
            Secret s = new Secret();
            int x = s.get();
        }
        """;
        BifyAssert.CompileError<BifyAttributeError>(code);
    }

    [Test]
    public void Class_MethodCallingAnotherMethod()
    {
        string code = """
        class MathHelper {
            public int square(int x) {
                return x * x;
            }
            public int sumOfSquares(int a, int b) {
                return this.square(a) + this.square(b);
            }
        }
        void test() {
            MathHelper m = new MathHelper();
            int res = m.sumOfSquares(3, 4);
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_ImplicitThisInMethod()
    {
        string code = """
        class Storage {
            public int value;
            public constructor(int v) {
                value = v;
            }
            public int getValue() {
                return value;
            }
        }
        void test() {
            Storage s = new Storage(99);
            int res = s.getValue();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_Inheritance_InheritAttributes()
    {
        string code = """
        class Animal {
            public int legs = 4;
        }
        class Dog : Animal {
            public int barkCount = 0;
        }
        void test() {
            Dog d = new Dog();
            int l = d.legs;
            int b = d.barkCount;
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_Inheritance_InheritMethods()
    {
        string code = """
        class Animal {
            public int getLegs() {
                return 4;
            }
        }
        class Dog : Animal {
        }
        void test() {
            Dog d = new Dog();
            int l = d.getLegs();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_Inheritance_VirtualOverride()
    {
        string code = """
        class Base {
            public virtual int value() {
                return 1;
            }
        }
        class Derived : Base {
            public override int value() {
                return 2;
            }
        }
        void test() {
            Derived d = new Derived();
            int v = d.value();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_Inheritance_PolymorphicDispatch()
    {
        string code = """
        class Base {
            public virtual int value() {
                return 1;
            }
        }
        class Derived : Base {
            public override int value() {
                return 2;
            }
        }
        void test() {
            Base b = new Derived();
            int v = b.value();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_Inheritance_MultiLevel()
    {
        string code = """
        class A {
            public virtual int f() {
                return 10;
            }
        }
        class B : A {
            public int g() {
                return 20;
            }
        }
        class C : B {
            public override int f() {
                return 30;
            }
        }
        void test() {
            A a = new C();
            int x = a.f();
            B b = new C();
            int y = b.g();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_Inheritance_ProtectedAccessInSubclass()
    {
        string code = """
        class Parent {
            protected int secret = 42;
            protected int getSecret() {
                return this.secret;
            }
        }
        class Child : Parent {
            public int testSecret() {
                return this.getSecret();
            }
        }
        void test() {
            Child c = new Child();
            int s = c.testSecret();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_Inheritance_ProtectedInaccessibleFromOutside()
    {
        string code = """
        class Parent {
            protected int secret = 42;
        }
        class Child : Parent {
        }
        void test() {
            Child c = new Child();
            int x = c.secret;
        }
        """;
        BifyAssert.CompileError<BifyAttributeError>(code);
    }

    [Test]
    public void Class_Inheritance_InvalidBaseThrows()
    {
        string code = """
        class Child : NonExistent {
        }
        void test() {
        }
        """;
        BifyAssert.CompileError<BifyTypeError>(code);
    }

    [Test]
    public void Class_Inheritance_AddDerivedMethod()
    {
        string code = """
        class Base {
            public int f() {
                return 1;
            }
        }
        class Derived : Base {
            public int g() {
                return 2;
            }
        }
        void test() {
            Derived d = new Derived();
            int a = d.f();
            int b = d.g();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_ARC_VariableCopyAndReassign()
    {
        string code = """
        class Node {
            public int value = 42;
        }
        void test() {
            Node a = new Node();
            Node b = a;
            Node c;
            c = b;
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_ARC_ScopeExitCleanup()
    {
        string code = """
        class Node {
            public int value = 1;
        }
        void test() {
            if (true) {
                Node local = new Node();
                int v = local.value;
            }
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_ARC_FunctionReturn()
    {
        string code = """
        class Node {
            public int value = 10;
        }
        Node createNode() {
            Node n = new Node();
            return n;
        }
        void test() {
            Node res = createNode();
            int v = res.value;
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_ARC_WeakReference()
    {
        string code = """
        class Node {
            public int val = 5;
        }
        void test() {
            Node strongRef = new Node();
            weak Node weakRef = strongRef;
            Node recovered = weakRef;
            int v = recovered.val;
        }
        """;
        AssertValidFunction(code);
    }
}
