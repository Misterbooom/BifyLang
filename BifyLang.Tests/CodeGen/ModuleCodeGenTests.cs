using BifyLang.CodeGen;
using BifyLang.CodeGen.Core;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class ModuleCodeGenTests : BifyTestBase
{
    [Test]
    public void ImportModule_InstantiateDottedClass_CompilesSuccessfully()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            string moduleFile = Path.Combine(tempDir, "mod.bify");
            File.WriteAllText(moduleFile, """
            class Greeter {
                void greet() {
                    printf("Hello from module");
                }
            }
            """);

            string mainFile = Path.Combine(tempDir, "main.bify");
            File.WriteAllText(mainFile, """
            import "./mod.bify";
            void main() {
                mod.Greeter g = new mod.Greeter();
                g.greet();
            }
            """);

            string outFile = Path.Combine(tempDir, "main.out");
            CompilationHelper.CompileFile(mainFile, outFile, CompilerFlags.NONE);
            Assert.That(File.Exists(outFile), Is.True);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
