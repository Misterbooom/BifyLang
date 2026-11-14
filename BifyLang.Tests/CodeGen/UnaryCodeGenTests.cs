#nullable enable
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class UnaryCodeGenTests : BifyTestBase
{
    [TestCase("int x = 42; int y = -x;", TestName = "Unary_NegateInt")]
    [TestCase("float f = 3.14; float g = -f;", TestName = "Unary_NegateFloat")]
    [TestCase("bool a = true; bool b = !a; bool c = !false;", TestName = "Unary_LogicalNot")]
    [TestCase("int x = 10; int y = ++x;", TestName = "Unary_PrefixIncrement")]
    [TestCase("int x = 10; int y = x++;", TestName = "Unary_PostfixIncrement")]
    [TestCase("int x = 10; int y = --x;", TestName = "Unary_PrefixDecrement")]
    [TestCase("int x = 10; int y = x--;", TestName = "Unary_PostfixDecrement")]
    [TestCase("int x = 0; x++; ++x; x--; --x;", TestName = "Unary_IncrementAsStatement")]
    [TestCase("float f = 1.5; f++; --f;", TestName = "Unary_FloatIncrement")]
    public void Unary_ValidOperations(string body)
    {
        AssertValidFunction($"void test() {{ {body} }}");
    }

    [Test]
    public void Unary_MutateConstThrows()
    {
        AssertCompileError<BifyTypeError>("void test() { const int c = 5; c++; }");
    }

    [Test]
    public void Unary_MutateUndefinedThrows()
    {
        AssertCompileError<BifyNameError>("void test() { missingVar++; }");
    }

    [Test]
    public void Unary_InvalidStatementThrows()
    {
        AssertCompileError<BifySyntaxError>("void test() { -5; }");
    }
}
