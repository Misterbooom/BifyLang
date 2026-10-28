#nullable enable
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;
using NUnit.Framework;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class CallCodeGenTests : BifyTestBase
{
    [Test]
    public void Call_VoidNoArgs()
    {
        AssertValidFunction("void foo() {} void test() { foo(); }");
    }

    [Test]
    public void Call_WithArgs()
    {
        AssertValidFunction("int add(int a, int b) { return a + b; } void test() { int res = add(10, 20); }");
    }

    [Test]
    public void Call_ImplicitArgCast()
    {
        AssertValidFunction("float scale(float x) { return x; } void test() { float res = scale(42); }");
    }

    [Test]
    public void Call_MultipleParametersOrder()
    {
        AssertValidFunction("int sub(int a, int b) { return a - b; } void test() { int res = sub(10, 3); }");
    }

    [Test]
    public void Call_NestedCalls()
    {
        AssertValidFunction("int inc(int x) { return x + 1; } void test() { int res = inc(inc(5)); }");
    }

    [Test]
    public void Call_AsStatement()
    {
        AssertValidFunction("void log(int val) {} void test() { log(42); }");
    }

    [Test]
    public void Call_ArgCountMismatchThrows()
    {
        AssertCompileError<BifyTypeError>("void foo(int a, int b) {} void test() { foo(1); }");
    }

    [Test]
    public void Call_UndefinedFunctionThrows()
    {
        AssertCompileError<BifyNameError>("void test() { undefinedFunc(); }");
    }
}
