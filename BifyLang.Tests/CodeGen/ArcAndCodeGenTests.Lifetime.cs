#nullable enable
namespace BifyLang.Tests.CodeGen;

public partial class ArcAndCodeGenTests
{
    [Test]
    public void LocalObject_EmitsSingleAllocAndRelease()
    {
        string code = CommonClasses + """
        func main() {
            val p: Plain = new Plain();
        }
        """;
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "main");

        Assert.That(Count(fn, "@gc_alloc("), Is.EqualTo(1));
        Assert.That(Count(fn, "@gc_retainStrong("), Is.EqualTo(0));
        Assert.That(Count(fn, "@gc_releaseStrong("), Is.EqualTo(1));
        AssertInOrder(fn, "@gc_alloc(", "@gc_releaseStrong(", "ret void");
    }

    [Test]
    [TestCase("new Base();")]
    [TestCase("d.getBase();")]
    public void UnconsumedTemporary_ReleasedImmediately(string discardStmt)
    {
        string code = CommonClasses + $$"""
        func main() {
            val d: Dummy = new Dummy();
            {{discardStmt}}
            printf("marker");
        }
        """;
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "main");

        AssertInOrder(fn, "@gc_releaseStrong(", "@printf", "@gc_releaseStrong(", "ret void");
    }

    [Test]
    public void Reassignment_RetainsNewBeforeReleasingOld()
    {
        string code = CommonClasses + """
        func main() {
            mut d: Dummy = new Dummy();
            val d2: Dummy = new Dummy();
            d = d2;
        }
        """;
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "main");

        AssertInOrder(
            fn,
            "@gc_retainStrong(ptr %d2",
            "%old_val = load ptr, ptr %d",
            "@gc_releaseStrong(ptr %old_val)",
            "store ptr %d2"
        );
    }

    [Test]
    public void WeakReference_LifecycleAndLifoCleanup()
    {
        string code = CommonClasses + """
        func main() {
            val d: Dummy = new Dummy();
            weak val w: Dummy = d;
        }
        """;
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "main");

        Assert.That(Count(fn, "@gc_retainWeak("), Is.EqualTo(1));
        AssertInOrder(
            fn,
            "@gc_retainWeak(",
            "%cleanup_w = load ptr, ptr %w",
            "@gc_releaseWeak(ptr %cleanup_w)",
            "%cleanup_d = load ptr, ptr %d",
            "@gc_releaseStrong(ptr %cleanup_d)",
            "ret void"
        );
    }

    [Test]
    public void WeakMemberAccess_UpgradesAndReleasesTemp()
    {
        string code = CommonClasses + """
        func main() {
            val d: Dummy = new Dummy();
            weak val w: Dummy = d;
            w.a = 4;
            w.getBase();
        }
        """;
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "main");

        Assert.That(Count(fn, "@gc_upgradeWeak("), Is.EqualTo(2));
        AssertInOrder(
            fn,
            "= call ptr @gc_upgradeWeak(",
            "store i32 4",
            "@gc_releaseStrong(",
            "= call ptr @gc_upgradeWeak(",
            "call ptr @main.Dummy_getBase(",
            "@gc_releaseStrong("
        );
    }

    [Test]
    public void ReturnNewObject_NoRedundantRetainRelease()
    {
        string code = CommonClasses + "func main() {}";
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "main.Dummy_getBase");

        Assert.That(Count(fn, "@gc_alloc("), Is.EqualTo(1));
        Assert.That(Count(fn, "@gc_retainStrong("), Is.EqualTo(0));
        Assert.That(Count(fn, "@gc_releaseStrong("), Is.EqualTo(0));
        AssertInOrder(fn, "%inst_ptr = call ptr @gc_alloc(", "ret ptr %inst_ptr");
    }

    [Test]
    public void ReturnLocalVariable_RetainsBeforeScopeCleanup()
    {
        string code = CommonClasses + "func main() {}";
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "main.Dummy_getLocal");

        Assert.That(Count(fn, "@gc_retainStrong("), Is.EqualTo(1));
        Assert.That(Count(fn, "@gc_releaseStrong("), Is.EqualTo(1));
        AssertInOrder(
            fn,
            "@gc_retainStrong(",
            "@gc_releaseStrong(",
            "ret ptr "
        );
    }

    [Test]
    public void LoopBreakAndContinue_UnwindsInnerScope()
    {
        string code = CommonClasses + """
        func main() {
            mut i: int = 0;
            while (i < 10) {
                val b: Base = new Base();
                if (i == 5) {
                    break;
                }
                i = i + 1;
                continue;
            }
        }
        """;
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "main");

        AssertInOrder(
            fn,
            "if.then:",
            "@gc_releaseStrong(",
            "br label %while.end",
            "if.merge:",
            "@gc_releaseStrong(",
            "br label %while.cond"
        );
    }

    [Test]
    public void FieldInitializer_ConsumesTemporaryWithoutRedundantRetain()
    {
        string code = CommonClasses + """
        class Holder {
            public val b: Base = new Base();
            constructor() {}
        }
        func main() {
            val h: Holder = new Holder();
        }
        """;
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "main");
        var ctor = ExtractFunctionIr(ir, "main.Holder_constructor");

        // The new Base() is initialized inside Holder.constructor without extra retainStrong/releaseStrong.
        Assert.That(Count(fn, "@gc_retainStrong("), Is.EqualTo(0));
        Assert.That(Count(fn, "@gc_alloc("), Is.EqualTo(1));
        Assert.That(Count(ctor, "@gc_retainStrong("), Is.EqualTo(0));
        Assert.That(Count(ctor, "@gc_alloc("), Is.EqualTo(1));
    }

    [Test]
    public void TypeHierarchy_ReferenceAndStructInvariants()
    {
        using var ctx = new BifyLang.CodeGen.Core.Context.CodeGenContext();
        var intType = BifyLang.CodeGen.Core.Types.Data.BifyType.CreateInt(ctx);
        var nullType = BifyLang.CodeGen.Core.Types.Data.BifyType.CreateNull(ctx);
        var structLlvm = ctx.Context.CreateNamedStruct("Vec2");
        var structType = new BifyLang.CodeGen.Core.Types.Data.StructType("Vec2", "main.Vec2", structLlvm, "main");
        var ptrType = LLVMSharp.Interop.LLVMTypeRef.CreatePointer(ctx.Context.Int8Type, 0);
        var classType = new BifyLang.CodeGen.Core.Types.Data.ClassType("Node", "main.Node", ptrType, structLlvm, "main");

        Assert.That(intType.IsReference, Is.False);
        Assert.That(nullType.IsReference, Is.False);
        Assert.That(structType.IsReference, Is.False);
        Assert.That(classType.IsReference, Is.True);
        Assert.That(classType, Is.InstanceOf<BifyLang.CodeGen.Core.Types.Data.ReferenceType>());

        Assert.That(classType.IsArcAssignableFrom(classType), Is.True);
        Assert.That(classType.IsArcAssignableFrom(nullType), Is.True);
        Assert.That(classType.IsArcAssignableFrom(intType), Is.False);
        Assert.That(structType.IsArcAssignableFrom(structType), Is.False);
        Assert.That(structType.IsArcAssignableFrom(nullType), Is.False);
    }
}
