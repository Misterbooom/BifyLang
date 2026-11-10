#nullable enable
using NUnit.Framework;

namespace BifyLang.Tests.CodeGen;

public partial class ArcAndCodeGenTests
{
    [Test]
    public void LocalObject_EmitsSingleAllocAndRelease()
    {
        string code = CommonClasses + """
        void main() {
            Plain p = new Plain();
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
        void main() {
            Dummy d = new Dummy();
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
        void main() {
            Dummy d = new Dummy();
            Dummy d2 = new Dummy();
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
        void main() {
            Dummy d = new Dummy();
            weak Dummy w = d;
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
        void main() {
            Dummy d = new Dummy();
            weak Dummy w = d;
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
            "call ptr @Dummy_getBase(",
            "@gc_releaseStrong("
        );
    }

    [Test]
    public void ReturnNewObject_NoRedundantRetainRelease()
    {
        string code = CommonClasses + "void main() {}";
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "Dummy_getBase");

        Assert.That(Count(fn, "@gc_alloc("), Is.EqualTo(1));
        Assert.That(Count(fn, "@gc_retainStrong("), Is.EqualTo(0));
        Assert.That(Count(fn, "@gc_releaseStrong("), Is.EqualTo(0));
        AssertInOrder(fn, "%inst_ptr = call ptr @gc_alloc(", "ret ptr %inst_ptr");
    }

    [Test]
    public void ReturnLocalVariable_RetainsBeforeScopeCleanup()
    {
        string code = CommonClasses + "void main() {}";
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "Dummy_getLocal");

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
        void main() {
            int i = 0;
            while (i < 10) {
                Base b = new Base();
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
            public Base b = new Base();
            constructor() {}
        }
        void main() {
            Holder h = new Holder();
        }
        """;
        var ir = CompileToIr(code);
        var fn = ExtractFunctionIr(ir, "main");

        // The new Base() is stored into Holder.b without extra retainStrong/releaseStrong in main.
        Assert.That(Count(fn, "@gc_retainStrong("), Is.EqualTo(0));
        Assert.That(Count(fn, "@gc_alloc("), Is.EqualTo(2)); // 1 for Holder, 1 for Base
    }
}
