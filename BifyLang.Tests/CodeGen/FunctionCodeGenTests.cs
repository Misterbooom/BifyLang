#nullable enable
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;
using LLVMSharp.Interop;
using NUnit.Framework;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class FunctionCodeGenTests : BifyTestBase
{
    [Test]
    public void CodeGen_VoidFunctionNoParams()
    {
        using var m = Bify.Compile("void testFunc() {}");
        m.AssertValid();

        var func = m.Function("testFunc");
        Assert.That(func.ParamsCount, Is.EqualTo(0));
        Assert.That(func.FirstBasicBlock.Handle, Is.Not.EqualTo(System.IntPtr.Zero));
    }

    [Test]
    public void CodeGen_VoidFunctionWithPrimitiveParams()
    {
        using var m = Bify.Compile("void compute(int a, float b, bool c) {}");
        m.AssertValid();

        var func = m.Function("compute");
        Assert.That(func.ParamsCount, Is.EqualTo(3));

        var paramA = func.GetParam(0);
        var paramB = func.GetParam(1);
        var paramC = func.GetParam(2);

        Assert.That(paramA.TypeOf.Kind, Is.EqualTo(LLVMTypeKind.LLVMIntegerTypeKind));
        Assert.That(paramA.TypeOf.IntWidth, Is.EqualTo(32));

        Assert.That(paramB.TypeOf.Kind, Is.EqualTo(LLVMTypeKind.LLVMFloatTypeKind));

        Assert.That(paramC.TypeOf.Kind, Is.EqualTo(LLVMTypeKind.LLVMIntegerTypeKind));
        Assert.That(paramC.TypeOf.IntWidth, Is.EqualTo(1));
    }

    [Test]
    public void CodeGen_VoidFunctionWithPointersAndArrays()
    {
        using var m = Bify.Compile("void processData(int* ptr, float[] list, CustomType* obj) {}");
        m.AssertValid();

        var func = m.Function("processData");
        Assert.That(func.ParamsCount, Is.EqualTo(3));
        Assert.That(func.GetParam(0).TypeOf.Kind, Is.EqualTo(LLVMTypeKind.LLVMPointerTypeKind));
        Assert.That(func.GetParam(1).TypeOf.Kind, Is.EqualTo(LLVMTypeKind.LLVMPointerTypeKind));
        Assert.That(func.GetParam(2).TypeOf.Kind, Is.EqualTo(LLVMTypeKind.LLVMPointerTypeKind));
    }

    [Test]
    public void CodeGen_MultipleFunctionsInOneModule()
    {
        string code = @"
void init() {}
void update(float dt) {}
void render(int* buffer, int width, int height) {}
";
        using var m = Bify.Compile(code);
        m.AssertValid();

        var initFunc = m.Function("init");
        var updateFunc = m.Function("update");
        var renderFunc = m.Function("render");

        Assert.That(initFunc.ParamsCount, Is.EqualTo(0));
        Assert.That(updateFunc.ParamsCount, Is.EqualTo(1));
        Assert.That(renderFunc.ParamsCount, Is.EqualTo(3));
    }

    [Test]
    public void CodeGen_FunctionParameterOrderPreserved()
    {
        using var m = Bify.Compile("void checkOrder(int first, float second, bool third) {}");
        m.AssertValid();

        var func = m.Function("checkOrder");
        Assert.That(func.ParamsCount, Is.EqualTo(3));

        Assert.That(func.GetParam(0).TypeOf.Kind, Is.EqualTo(LLVMTypeKind.LLVMIntegerTypeKind));
        Assert.That(func.GetParam(0).TypeOf.IntWidth, Is.EqualTo(32));

        Assert.That(func.GetParam(1).TypeOf.Kind, Is.EqualTo(LLVMTypeKind.LLVMFloatTypeKind));

        Assert.That(func.GetParam(2).TypeOf.Kind, Is.EqualTo(LLVMTypeKind.LLVMIntegerTypeKind));
        Assert.That(func.GetParam(2).TypeOf.IntWidth, Is.EqualTo(1));
    }

    [Test]
    public void CodeGen_DuplicateParameterNameThrows()
    {
        AssertCompileError<BifyNameError>("void badFunc(int x, float x) {}");
    }

    [Test]
    public void CodeGen_NonVoidWithoutReturnThrows()
    {
        AssertCompileError<BifyTypeError>("int badFunc() {}");
    }
}
