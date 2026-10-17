#nullable enable
using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen;
using BifyLang.CodeGen.Core;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.Tests;

internal static class ClassCodeGenTests
{
    public static Dictionary<string, Func<bool>> GetTests() => new()
    {
        { "Class_EmptyClassAndInstantiation", TestEmptyClassAndInstantiation },
        { "Class_AttributeDefaultValuesAndRead", TestAttributeDefaultValuesAndRead },
        { "Class_ConstructorInitializesAttributes", TestConstructorInitializesAttributes },
        { "Class_MethodCallAndReturn", TestMethodCallAndReturn },
        { "Class_MethodAccessAndMutateThis", TestMethodAccessAndMutateThis },
        { "Class_AttributeAssignmentFromOutside", TestAttributeAssignmentFromOutside },
        { "Class_PrivateAttributeThrowsOutside", TestPrivateAttributeThrowsOutside },
        { "Class_PrivateMethodThrowsOutside", TestPrivateMethodThrowsOutside },
        { "Class_MethodCallingAnotherMethod", TestMethodCallingAnotherMethod },
        { "Class_ImplicitThisInMethod", TestImplicitThisInMethod },
        { "Class_Inheritance_InheritAttributes", TestInheritAttributes },
        { "Class_Inheritance_InheritMethods", TestInheritMethods },
        { "Class_Inheritance_VirtualOverride", TestVirtualOverride },
        { "Class_Inheritance_PolymorphicDispatch", TestPolymorphicDispatch },
        { "Class_Inheritance_MultiLevel", TestMultiLevelInheritance },
        { "Class_Inheritance_ProtectedAccessInSubclass", TestProtectedAccessInSubclass },
        { "Class_Inheritance_ProtectedInaccessibleFromOutside", TestProtectedInaccessibleFromOutside },
        { "Class_Inheritance_InvalidBaseThrows", TestInvalidBaseThrows },
        { "Class_Inheritance_AddDerivedMethod", TestAddDerivedMethod },
    };

    private static (LLVMModuleRef Module, CodeGenContext Context) CompileCode(string code)
    {
        MyLexer lexer = new(code);
        List<Token> tokens = lexer.Tokenize();
        string[] lines = code.Split('\n');
        AstTree tree = new(lines);
        AstModule moduleAst = tree.ParseTokens(tokens);

        CodeGenContext ctx = new();
        LLVMModuleRef module = ctx.GenerateModule(moduleAst);
        return (module, ctx);
    }

    private static bool Verify(string code)
    {
        var (module, ctx) = CompileCode(code);
        using (ctx)
        {
            if (!BifyCompiler.VerifyModule(module, out string? error))
            {
                Console.WriteLine($"VerifyModule error: {error}");
                Console.WriteLine($"Module IR:\n{module}");
                return false;
            }

            var func = module.GetNamedFunction("test");
            return func.Handle != IntPtr.Zero;
        }
    }

    private static bool VerifyThrows(string code, Type expectedErrorType)
    {
        Traceback.Instance.Clear();
        Traceback.Instance.Catch(expectedErrorType);
        try
        {
            var (_, ctx) = CompileCode(code);
            ctx.Dispose();
            var err = Traceback.Instance.GetError();
            return err != null && err.GetType() == expectedErrorType;
        }
        catch (ApplicationException)
        {
            return true;
        }
        catch (Exception)
        {
            return true;
        }
        finally
        {
            Traceback.Instance.TrackPop();
        }
    }

    private static bool TestEmptyClassAndInstantiation()
    {
        string code = """
        class Dummy {
        }
        void test() {
            Dummy d = new Dummy();
        }
        """;
        return Verify(code);
    }

    private static bool TestAttributeDefaultValuesAndRead()
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
        return Verify(code);
    }

    private static bool TestConstructorInitializesAttributes()
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
        return Verify(code);
    }

    private static bool TestMethodCallAndReturn()
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
        return Verify(code);
    }

    private static bool TestMethodAccessAndMutateThis()
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
        return Verify(code);
    }

    private static bool TestAttributeAssignmentFromOutside()
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
        return Verify(code);
    }

    private static bool TestPrivateAttributeThrowsOutside()
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
        return VerifyThrows(code, typeof(BifyAttributeError));
    }

    private static bool TestPrivateMethodThrowsOutside()
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
        return VerifyThrows(code, typeof(BifyAttributeError));
    }

    private static bool TestMethodCallingAnotherMethod()
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
        return Verify(code);
    }

    private static bool TestImplicitThisInMethod()
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
        return Verify(code);
    }

    private static bool TestInheritAttributes()
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
        return Verify(code);
    }

    private static bool TestInheritMethods()
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
        return Verify(code);
    }

    private static bool TestVirtualOverride()
    {
        string code = """
        class Base {
            public int value() {
                return 1;
            }
        }
        class Derived : Base {
            public int value() {
                return 2;
            }
        }
        void test() {
            Derived d = new Derived();
            int v = d.value();
        }
        """;
        return Verify(code);
    }

    private static bool TestPolymorphicDispatch()
    {
        string code = """
        class Base {
            public int value() {
                return 1;
            }
        }
        class Derived : Base {
            public int value() {
                return 2;
            }
        }
        void test() {
            Base b = new Derived();
            int v = b.value();
        }
        """;
        return Verify(code);
    }

    private static bool TestMultiLevelInheritance()
    {
        string code = """
        class A {
            public int f() {
                return 10;
            }
        }
        class B : A {
            public int g() {
                return 20;
            }
        }
        class C : B {
            public int f() {
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
        return Verify(code);
    }

    private static bool TestProtectedAccessInSubclass()
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
        return Verify(code);
    }

    private static bool TestProtectedInaccessibleFromOutside()
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
        return VerifyThrows(code, typeof(BifyAttributeError));
    }

    private static bool TestInvalidBaseThrows()
    {
        string code = """
        class Child : NonExistent {
        }
        void test() {
        }
        """;
        return VerifyThrows(code, typeof(BifyTypeError));
    }

    private static bool TestAddDerivedMethod()
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
        return Verify(code);
    }
}
