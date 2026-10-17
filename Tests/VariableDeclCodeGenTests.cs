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

internal static class VariableDeclCodeGenTests
{
    public static Dictionary<string, Func<bool>> GetTests() => new()
    {
        { "VarDeclCodeGen_VarInferredInt", TestVarInferredInt },
        { "VarDeclCodeGen_VarInferredFloat", TestVarInferredFloat },
        { "VarDeclCodeGen_VarInferredBool", TestVarInferredBool },
        { "VarDeclCodeGen_VarInferredString", TestVarInferredString },
        { "VarDeclCodeGen_VarUninitializedThrows", TestVarUninitializedThrows },
        { "VarDeclCodeGen_ImplicitCastIntToFloat", TestImplicitCastIntToFloat },
        { "VarDeclCodeGen_ImplicitCastFloatToDouble", TestImplicitCastFloatToDouble },
        { "VarDeclCodeGen_ImplicitCastFloatToIntThrows", TestImplicitCastFloatToIntThrows },
        { "VarDeclCodeGen_ImplicitCastBoolToInt", TestImplicitCastBoolToInt },
        { "VarDeclCodeGen_ImplicitCastIntToBool", TestImplicitCastIntToBool },
        { "VarDeclCodeGen_ImplicitCastNullToPointer", TestImplicitCastNullToPointer },
        { "VarDeclCodeGen_IncompatibleTypesThrows", TestIncompatibleTypesThrows },
        { "VarDeclCodeGen_ExplicitCastFloatToInt", TestExplicitCastFloatToInt },
        { "VarDeclCodeGen_ExplicitCastIntToFloat", TestExplicitCastIntToFloat },
        { "VarDeclCodeGen_ExplicitCastChar", TestExplicitCastChar },
        { "VarDeclCodeGen_ExplicitCastIncompatibleThrows", TestExplicitCastIncompatibleThrows },
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

    static bool TestVarInferredInt()
    {
        string code = @"
void test() {
    var x = 42;
}
";
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

    static bool TestVarInferredFloat()
    {
        string code = @"
void test() {
    var pi = 3.14;
}
";
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

    static bool TestVarInferredBool()
    {
        string code = @"
void test() {
    var flag = true;
}
";
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

    static bool TestVarInferredString()
    {
        string code = @"
void test() {
    var message = ""hello world"";
}
";
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

    static bool TestVarUninitializedThrows()
    {
        string code = @"
void test() {
    var uninit;
}
";
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

    static bool TestImplicitCastIntToFloat()
    {
        string code = @"
void test() {
    float f = 42;
}
";
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

    static bool TestImplicitCastFloatToDouble()
    {
        string code = @"
void test() {
    double d = 3.14;
}
";
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

    static bool TestImplicitCastFloatToIntThrows()
    {
        string code = @"
void test() {
    int i = 3.14;
}
";
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

    static bool TestImplicitCastBoolToInt()
    {
        string code = @"
void test() {
    int i = true;
}
";
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

    static bool TestImplicitCastIntToBool()
    {
        string code = @"
void test() {
    bool b = 1;
}
";
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

    static bool TestImplicitCastNullToPointer()
    {
        string code = @"
void test() {
    int* ptr = null;
}
";
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

    static bool TestIncompatibleTypesThrows()
    {
        string code = @"
void test() {
    int x = ""cannot convert string to int"";
}
";
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

    static bool TestExplicitCastFloatToInt()
    {
        string code = @"
void test() {
    int x = (int)3.14;
}
";
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

    static bool TestExplicitCastIntToFloat()
    {
        string code = @"
void test() {
    float x = (float)42;
}
";
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

    static bool TestExplicitCastChar()
    {
        string code = @"
void test() {
    char c = (char)65;
    int i = (int)c;
}
";
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

    static bool TestExplicitCastIncompatibleThrows()
    {
        string code = @"
void test() {
    float x = (float)""cannot cast string to float"";
}
";
        Traceback.Instance.Clear();
        Traceback.Instance.Catch(typeof(BifyCastError));
        try
        {
            var (_, ctx) = CompileCode(code);
            ctx.Dispose();
            var err = Traceback.Instance.GetError();
            return err is BifyCastError;
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
