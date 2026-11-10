#nullable enable
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;
using NUnit.Framework;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class NewAndClassBuilderTests : BifyTestBase
{
    [Test]
    public void New_InstantiatesEmptyClass()
    {
        string code = """
        class Dummy {}
        void test() {
            Dummy d = new Dummy();
        }
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();

        var dummyType = (ClassType)m.Context.TypeRegistry.GetBifyType("Dummy");
        Assert.That(dummyType.VTableGlobal.Handle, Is.Not.EqualTo(System.IntPtr.Zero));
    }

    [Test]
    public void New_InitializesFieldsWithDefaults()
    {
        string code = """
        class Config {
            public int port = 8080;
            public float ratio = 1.5;
            public bool active = true;
        }
        void test() {
            Config c = new Config();
        }
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();
    }

    [Test]
    public void New_InitializesUnassignedFieldsToZero()
    {
        string code = """
        class Node {
            public int value;
            public Node next;
        }
        void test() {
            Node n = new Node();
        }
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();
    }

    [Test]
    public void New_InvokesConstructorWithArguments()
    {
        string code = """
        class Service {
            public int port;
            public constructor(int p) {
            }
        }
        void test() {
            Service s = new Service(9000);
        }
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();
    }

    [Test]
    public void New_DerivedClassInitializesInheritedAndOwnFields()
    {
        string code = """
        class Base {
            public int a = 1;
        }
        class Derived : Base {
            public int b = 2;
        }
        void test() {
            Derived d = new Derived();
        }
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();
    }

    [Test]
    public void New_NonClassType_Throws() =>
        AssertCompileError<BifyTypeError>("void test() { int x = new int(); }");

    [Test]
    public void New_ConstructorArgumentMismatch_Throws() =>
        AssertCompileError<BifyTypeError>("""
        class Point {
            public constructor(int a, int b) {}
        }
        void test() {
            Point p = new Point(10);
        }
        """);

    [Test]
    public void New_ArgumentsPassedToClassWithoutConstructor_Throws() =>
        AssertCompileError<BifyTypeError>("""
        class Empty {}
        void test() {
            Empty e = new Empty(1, 2);
        }
        """);
}
