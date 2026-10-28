#nullable enable
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;
using NUnit.Framework;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class AssignmentCodeGenTests : BifyTestBase
{
    [TestCase("int x = 5; x = 10;", TestName = "Assign_BasicInt")]
    [TestCase("int x = 10; x += 5;", TestName = "Assign_CompoundAdd")]
    [TestCase("int x = 10; x -= 4;", TestName = "Assign_CompoundSub")]
    [TestCase("int x = 3; x *= 4;", TestName = "Assign_CompoundMul")]
    [TestCase("int x = 20; x /= 5;", TestName = "Assign_CompoundDiv")]
    [TestCase("float f = 2.5; f += 1.5; f -= 0.5; f *= 2.0; f /= 2.0;", TestName = "Assign_FloatCompound")]
    public void Assign_ValidOperations(string body)
    {
        AssertValidFunction($"void test() {{ {body} }}");
    }

    [Test]
    public void Assign_AssignToConstThrows()
    {
        AssertCompileError<BifyTypeError>("void test() { const int c = 42; c = 100; }");
    }

    [Test]
    public void Assign_AssignToUndefinedThrows()
    {
        AssertCompileError<BifyNameError>("void test() { missingVariable = 10; }");
    }

    [Test]
    public void Assign_IncompatibleTypeThrows()
    {
        AssertCompileError<BifyTypeError>("void test() { int x = 10; x = \"cannot assign string to int\"; }");
    }
}
