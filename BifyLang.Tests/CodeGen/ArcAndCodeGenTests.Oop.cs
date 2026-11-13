#nullable enable
using NUnit.Framework;

namespace BifyLang.Tests.CodeGen;

public partial class ArcAndCodeGenTests
{
    [Test]
    public void Destructor_ReleasesClassFieldsAndCallsParentDestructor()
    {
        string code = CommonClasses + "void main() {}";
        var ir = CompileToIr(code);
        var dummyDtor = ExtractFunctionIr(ir, "main.Dummy_destructor");
        var baseDtor = ExtractFunctionIr(ir, "main.Base_destructor");

        // Dummy_destructor releases own strong 'child' and weak 'wchild', then chains to Base_destructor
        Assert.That(Count(dummyDtor, "@gc_releaseStrong("), Is.EqualTo(1));
        Assert.That(Count(dummyDtor, "@gc_releaseWeak("), Is.EqualTo(1));
        AssertInOrder(
            dummyDtor,
            "@gc_releaseStrong(",
            "@gc_releaseWeak(",
            "call void @main.Base_destructor(ptr %0)",
            "ret void"
        );

        // Inherited 'leaf' field is NOT released in Dummy_destructor (released in Base_destructor)
        Assert.That(dummyDtor.Contains("%leaf"), Is.False);
        Assert.That(Count(baseDtor, "@gc_releaseStrong("), Is.EqualTo(1));
        Assert.That(baseDtor.Contains("%leaf"), Is.True);
    }

    [Test]
    public void VTable_GlobalsContainDestructorAndOverrides()
    {
        string code = CommonClasses + "void main() {}";
        var ir = CompileToIr(code);

        // Base vtable: slot 0 = destructor, slot 1 = getX
        Assert.That(ir, Does.Contain("@main.Base.vtable = internal constant [2 x ptr] [ptr @main.Base_destructor, ptr @main.Base_getX]"));

        // Dummy vtable: slot 0 = overridden destructor, slot 1 = overridden getX
        Assert.That(ir, Does.Contain("@main.Dummy.vtable = internal constant [2 x ptr] [ptr @main.Dummy_destructor, ptr @main.Dummy_getX]"));

        // Plain vtable: slot 0 = own destructor, slot 1 = inherited Base_getX
        Assert.That(ir, Does.Contain("@main.Plain.vtable = internal constant [2 x ptr] [ptr @main.Plain_destructor, ptr @main.Base_getX]"));
    }

    [Test]
    public void VirtualCall_DispatchesThroughVTable()
    {
        string code = CommonClasses + """
        int test() {
            Dummy d = new Dummy();
            return d.getX();
        }
        void main() {}
        """;
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "main.test");

        AssertInOrder(
            fn,
            "%vptr_slot = getelementptr inbounds nuw %Dummy",
            "i32 0, i32 0",
            "%vtable = load ptr, ptr %vptr_slot",
            "%slot_ptr = getelementptr ptr, ptr %vtable, i32 1",
            "%getX_fn = load ptr, ptr %slot_ptr",
            "= call i32 %getX_fn("
        );
        // Ensure it did NOT emit a direct static call to Dummy_getX
        Assert.That(fn, Does.Not.Contain("call i32 @main.Dummy_getX("));
    }

    [Test]
    public void NonVirtualCall_IsDirect()
    {
        string code = CommonClasses + """
        void test() {
            Dummy d = new Dummy();
            d.getBase();
        }
        void main() {}
        """;
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "main.test");

        Assert.That(fn, Does.Contain("call ptr @main.Dummy_getBase("));
        Assert.That(fn, Does.Not.Contain("%vptr_slot"));
    }

    [Test]
    public void NewInstance_PassesClassVTableToGcAlloc()
    {
        string code = CommonClasses + """
        void main() {
            Base b = new Base();
            Dummy d = new Dummy();
        }
        """;
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "main");

        Assert.That(fn, Does.Contain("call ptr @gc_alloc(i64 ptrtoint (ptr getelementptr (%Base, ptr null, i32 1) to i64), ptr @main.Base.vtable)"));
        Assert.That(fn, Does.Contain("call ptr @gc_alloc(i64 ptrtoint (ptr getelementptr (%Dummy, ptr null, i32 1) to i64), ptr @main.Dummy.vtable)"));
    }

    [Test]
    public void ExplicitCast_DowncastUpdatesCompileTimeClassTypeWithoutPointerCast()
    {
        string code = CommonClasses + """
        int test() {
            Base b = new Dummy();
            Dummy d = (Dummy)b;
            return d.a;
        }
        void main() {}
        """;
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "main.test");
        Assert.That(fn, Does.Contain("getelementptr inbounds nuw %Dummy"));
        Assert.That(fn, Does.Not.Contain("cast_ptr"));
    }

    [Test]
    public void ExplicitCast_UnrelatedClassesThrowCastError()
    {
        string code = CommonClasses + """
        void main() {
            Leaf l = new Leaf();
            Dummy d = (Dummy)l;
        }
        """;
        AssertCompileError<BifyLang.Exceptions.BifyCastError>(code);
    }

    [Test]
    public void Method_ImplicitIntToFloatReturn_UsesSignatureReturnType()
    {
        string code = """
        class MathHelper {
            public float asFloat(int x) {
                return x;
            }
        }
        void main() {}
        """;
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "main.MathHelper_asFloat");
        Assert.That(fn, Does.Contain("sitofp i32"));
        Assert.That(fn, Does.Contain("to float"));
        Assert.That(fn, Does.Contain("ret float"));
    }
}
