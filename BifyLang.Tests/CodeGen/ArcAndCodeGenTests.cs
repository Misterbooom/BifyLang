#nullable enable
using System.Text.RegularExpressions;
using BifyLang.CodeGen;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public partial class ArcAndCodeGenTests : BifyTestBase
{
    protected const string CommonClasses = """
        class Leaf {
            constructor() {}
        }
        class Base {
            public Leaf leaf;
            constructor() {}
            public virtual int getX() {
                return 0;
            }
        }
        class Dummy : Base {
            public int a = 5;
            public Base child;
            weak Base wchild;
            constructor() {}
            public override int getX() {
                return a;
            }
            public Base getBase() {
                return new Base();
            }
            public Base getLocal() {
                Base b = new Base();
                return b;
            }
        }
        class Plain : Base {
            constructor() {}
        }
        """;

    protected static string CompileToIr(string source)
    {
        using var m = Bify.Compile(source);
        if (!BifyCompiler.VerifyModule(m.Module, out var error))
        {
            Assert.Fail($"Module verification failed: {error}\n\nIR:\n{m.IR}");
        }
        return m.Module.PrintToString();
    }

    protected static string ExtractFunctionIr(string fullIr, string funcName)
    {
        var pattern = $@"(?m)^define\s+[^{{\n]*@{Regex.Escape(funcName)}\([^{{\n]*\{{(?:[^{{}}]*|\{{[^{{}}]*\}})*\}}";
        var match = Regex.Match(fullIr, pattern);
        if (!match.Success)
        {
            Assert.Fail($"Function '@{funcName}' not found in IR:\n{fullIr}");
        }
        return match.Value;
    }

    protected static int Count(string text, string needle)
    {
        int count = 0;
        int idx = 0;
        while ((idx = text.IndexOf(needle, idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }

    protected static void AssertInOrder(string text, params string[] fragments)
    {
        int lastIdx = -1;
        for (int i = 0; i < fragments.Length; i++)
        {
            var frag = fragments[i];
            int currentIdx = text.IndexOf(frag, lastIdx == -1 ? 0 : lastIdx + 1, StringComparison.Ordinal);
            if (currentIdx == -1)
            {
                Assert.Fail($"Expected fragment [{i}] '{frag}' in order, but it was not found after index {lastIdx}.\nText:\n{text}");
            }
            lastIdx = currentIdx;
        }
    }

    protected static string CaptureSsa(string text, string pattern)
    {
        var match = Regex.Match(text, pattern);
        if (!match.Success || match.Groups.Count < 2)
        {
            Assert.Fail($"Could not capture SSA variable with pattern '{pattern}'.\nText:\n{text}");
        }
        return match.Groups[1].Value;
    }
}
