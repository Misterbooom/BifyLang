#nullable enable
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class VariableDeclCodeGenTests : BifyTestBase
{
    [TestCase("var x = 42;", TestName = "VarDecl_InferredInt")]
    [TestCase("var pi = 3.14;", TestName = "VarDecl_InferredFloat")]
    [TestCase("var flag = true;", TestName = "VarDecl_InferredBool")]
    [TestCase("var message = \"hello world\";", TestName = "VarDecl_InferredString")]
    public void VarDecl_InferredTypes(string decl)
    {
        AssertValidFunction($"void test() {{ {decl} }}");
    }

    [TestCase("float f = 42;", TestName = "VarDecl_ImplicitCast_IntToFloat")]
    [TestCase("double d = 3.14;", TestName = "VarDecl_ImplicitCast_FloatToDouble")]
    [TestCase("int i = true;", TestName = "VarDecl_ImplicitCast_BoolToInt")]
    [TestCase("bool b = 1;", TestName = "VarDecl_ImplicitCast_IntToBool")]
    [TestCase("int* ptr = null;", TestName = "VarDecl_ImplicitCast_NullToPointer")]
    public void VarDecl_ImplicitCasts(string decl)
    {
        AssertValidFunction($"void test() {{ {decl} }}");
    }

    [TestCase("int x = (int)3.14;", TestName = "VarDecl_ExplicitCast_FloatToInt")]
    [TestCase("float x = (float)42;", TestName = "VarDecl_ExplicitCast_IntToFloat")]
    [TestCase("char c = (char)65; int i = (int)c;", TestName = "VarDecl_ExplicitCast_CharAndInt")]
    public void VarDecl_ExplicitCasts(string body)
    {
        AssertValidFunction($"void test() {{ {body} }}");
    }

    [TestCase("var uninit;", TestName = "VarDecl_Error_VarUninitialized")]
    [TestCase("int i = 3.14;", TestName = "VarDecl_Error_ImplicitFloatToInt")]
    [TestCase("int x = \"cannot convert string to int\";", TestName = "VarDecl_Error_IncompatibleTypes")]
    public void VarDecl_TypeErrors(string decl)
    {
        AssertCompileError($"void test() {{ {decl} }}", DiagnosticKind.Type);
    }

    [Test]
    public void VarDeclCodeGen_ExplicitCastIncompatibleThrows()
    {
        AssertCompileError("void test() { float x = (float)\"cannot cast string to float\"; }", DiagnosticKind.Cast);
    }

    [Test]
    public void VarDecl_NullFlagNode_DoesNotThrow()
    {
        using var tc = new ExpressionTestContext();
        tc.Ctx.FunctionEntry = tc.Function.FirstBasicBlock;
        var varId = new Ast.AstIdentifier(new global::BifyLang.Lexer.Token(global::BifyLang.Lexer.TokenType.IDENTIFIER, "x"), "x");
        var val = new Ast.AstInteger(new global::BifyLang.Lexer.Token(global::BifyLang.Lexer.TokenType.NUMBER, "7"), 7);
        var assign = new Ast.AstAssignment(new global::BifyLang.Lexer.Token(global::BifyLang.Lexer.TokenType.EQ, "="), varId, val);
        var typeId = new Ast.AstIdentifier(new global::BifyLang.Lexer.Token(global::BifyLang.Lexer.TokenType.IDENTIFIER, "int"), "int");
        var decl = new Ast.AstVarDecl(varId.Token, assign, typeId, flagNode: null);

        Assert.DoesNotThrow(() => new BifyLang.CodeGen.Statements.VariableDeclGen(tc.Ctx).EmitVariableDecl(decl));
    }

    [Test]
    public void VarDecl_PoisonRecovery_SuppressesCascadingUndefinedVariableAndContinues()
    {
        string code = """
        void test() {
            int a = ;
            int b = a + 1;
            int c = "bad";
        }
        """;
        var diagnostics = new DiagnosticBag();
        Bify.Compile(code, diagnostics);

        Assert.That(diagnostics.HasErrors, Is.True);
        Assert.That(diagnostics.Diagnostics.Any(d => d.Kind == DiagnosticKind.Syntax && d.Location.LineNumber == 2), Is.True);
        Assert.That(diagnostics.Diagnostics.Any(d => d.Kind == DiagnosticKind.Type && d.Location.LineNumber == 4), Is.True);
        Assert.That(diagnostics.Diagnostics.Any(d => d.Message.Contains("is not defined")), Is.False);
    }
}
