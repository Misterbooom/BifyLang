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

internal static class CallCodeGenTests
{
    public static Dictionary<string, Func<bool>> GetTests() => new()
    {
        { "Call_VoidNoArgs", TestCallVoidNoArgs },
        { "Call_WithArgs", TestCallWithArgs },
        { "Call_ImplicitArgCast", TestCallImplicitArgCast },
        { "Call_MultipleParametersOrder", TestCallMultipleParametersOrder },
        { "Call_NestedCalls", TestCallNestedCalls },
        { "Call_AsStatement", TestCallAsStatement },
        { "Call_ArgCountMismatchThrows", TestCallArgCountMismatchThrows },
        { "Call_UndefinedFunctionThrows", TestCallUndefinedFunctionThrows },
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

    private static bool VerifyThrows(string code, Type expectedErrorType)
    {
        Traceback.Instance.Clear();
        Traceback.Instance.Catch(expectedErrorType);
        try
        {
            var (_, ctx) = CompileCode(code);
            ctx.Dispose();
            var err = Traceback.Instance.GetError();
            return err != null && err.GetType() == expectedErrorType;
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

    static bool TestCallVoidNoArgs()
    {
        string code = @"
void foo() {}
void test() {
    foo();
}
";
        return Verify(code);
    }

    static bool TestCallWithArgs()
    {
        string code = @"
int add(int a, int b) {
    return a + b;
}
void test() {
    int res = add(10, 20);
}
";
        return Verify(code);
    }

    static bool TestCallImplicitArgCast()
    {
        string code = @"
float scale(float x) {
    return x;
}
void test() {
    float res = scale(42);
}
";
        return Verify(code);
    }

    static bool TestCallMultipleParametersOrder()
    {
        string code = @"
int sub(int a, int b) {
    return a - b;
}
void test() {
    int res = sub(10, 3);
}
";
        return Verify(code);
    }

    static bool TestCallNestedCalls()
    {
        string code = @"
int inc(int x) {
    return x + 1;
}
void test() {
    int res = inc(inc(5));
}
";
        return Verify(code);
    }

    static bool TestCallAsStatement()
    {
        string code = @"
void log(int val) {}
void test() {
    log(42);
}
";
        return Verify(code);
    }

    static bool TestCallArgCountMismatchThrows()
    {
        string code = @"
void foo(int a, int b) {}
void test() {
    foo(1);
}
";
        return VerifyThrows(code, typeof(BifyTypeError));
    }

    static bool TestCallUndefinedFunctionThrows()
    {
        string code = @"
void test() {
    undefinedFunc();
}
";
        return VerifyThrows(code, typeof(BifyNameError));
    }
}
