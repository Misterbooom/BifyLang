#nullable enable
using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen;
using BifyLang.CodeGen.Core;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.Tests;

internal static class UnaryCodeGenTests
{
    public static Dictionary<string, Func<bool>> GetTests() => new()
    {
        { "Unary_NegateInt", TestUnaryNegateInt },
        { "Unary_NegateFloat", TestUnaryNegateFloat },
        { "Unary_LogicalNot", TestUnaryLogicalNot },
        { "Unary_PrefixIncrement", TestUnaryPrefixIncrement },
        { "Unary_PostfixIncrement", TestUnaryPostfixIncrement },
        { "Unary_PrefixDecrement", TestUnaryPrefixDecrement },
        { "Unary_PostfixDecrement", TestUnaryPostfixDecrement },
        { "Unary_IncrementAsStatement", TestUnaryIncrementAsStatement },
        { "Unary_FloatIncrement", TestUnaryFloatIncrement },
        { "Unary_MutateConstThrows", TestUnaryMutateConstThrows },
        { "Unary_MutateUndefinedThrows", TestUnaryMutateUndefinedThrows },
        { "Unary_InvalidStatementThrows", TestUnaryInvalidStatementThrows },
    };

    private static (LLVMModuleRef Module, CodeGenContext Context) CompileCode(string code)
    {
        MyLexer lexer = new(code);
        List<Token> tokens = lexer.Tokenize();
        string[] lines = code.Split('\n');
        AstTree tree = new(lines);
        AstModule moduleAst = tree.ParseTokens(tokens);

        CodeGenContext ctx = new();
        LLVMModuleRef module = ctx.GenerateModule(moduleAst);
        return (module, ctx);
    }

    private static bool Verify(string code)
    {
        var (module, ctx) = CompileCode(code);
        using (ctx)
        {
            if (!BifyCompiler.VerifyModule(module, out string? error))
            {
                Console.WriteLine($"VerifyModule error: {error}");
                return false;
            }

            var func = module.GetNamedFunction("test");
            return func.Handle != IntPtr.Zero;
        }
    }

    static bool TestUnaryNegateInt()
    {
        string code = @"
void test() {
    int x = 42;
    int y = -x;
}
";
        return Verify(code);
    }

    static bool TestUnaryNegateFloat()
    {
        string code = @"
void test() {
    float f = 3.14;
    float g = -f;
}
";
        return Verify(code);
    }

    static bool TestUnaryLogicalNot()
    {
        string code = @"
void test() {
    bool a = true;
    bool b = !a;
    bool c = !false;
}
";
        return Verify(code);
    }

    static bool TestUnaryPrefixIncrement()
    {
        string code = @"
void test() {
    int x = 10;
    int y = ++x;
}
";
        return Verify(code);
    }

    static bool TestUnaryPostfixIncrement()
    {
        string code = @"
void test() {
    int x = 10;
    int y = x++;
}
";
        return Verify(code);
    }

    static bool TestUnaryPrefixDecrement()
    {
        string code = @"
void test() {
    int x = 10;
    int y = --x;
}
";
        return Verify(code);
    }

    static bool TestUnaryPostfixDecrement()
    {
        string code = @"
void test() {
    int x = 10;
    int y = x--;
}
";
        return Verify(code);
    }

    static bool TestUnaryIncrementAsStatement()
    {
        string code = @"
void test() {
    int x = 0;
    x++;
    ++x;
    x--;
    --x;
}
";
        return Verify(code);
    }

    static bool TestUnaryFloatIncrement()
    {
        string code = @"
void test() {
    float f = 1.5;
    f++;
    --f;
}
";
        return Verify(code);
    }

    static bool TestUnaryMutateConstThrows()
    {
        string code = @"
void test() {
    const int c = 5;
    c++;
}
";
        Traceback.Instance.Clear();
        Traceback.Instance.Catch(typeof(BifyTypeError));
        try
        {
            var (_, ctx) = CompileCode(code);
            ctx.Dispose();
            var err = Traceback.Instance.GetError();
            return err is BifyTypeError;
        }
        catch (ApplicationException)
        {
            return true;
        }
        catch (Exception)
        {
            return true;
        }
        finally
        {
            Traceback.Instance.TrackPop();
        }
    }

    static bool TestUnaryMutateUndefinedThrows()
    {
        string code = @"
void test() {
    missingVar++;
}
";
        Traceback.Instance.Clear();
        Traceback.Instance.Catch(typeof(BifyNameError));
        try
        {
            var (_, ctx) = CompileCode(code);
            ctx.Dispose();
            var err = Traceback.Instance.GetError();
            return err is BifyNameError;
        }
        catch (ApplicationException)
        {
            return true;
        }
        catch (Exception)
        {
            return true;
        }
        finally
        {
            Traceback.Instance.TrackPop();
        }
    }

    static bool TestUnaryInvalidStatementThrows()
    {
        string code = @"
void test() {
    -5;
}
";
        Traceback.Instance.Clear();
        Traceback.Instance.Catch(typeof(BifySyntaxError));
        try
        {
            var (_, ctx) = CompileCode(code);
            ctx.Dispose();
            var err = Traceback.Instance.GetError();
            return err is BifySyntaxError;
        }
        catch (ApplicationException)
        {
            return true;
        }
        catch (Exception)
        {
            return true;
        }
        finally
        {
            Traceback.Instance.TrackPop();
        }
    }
}
