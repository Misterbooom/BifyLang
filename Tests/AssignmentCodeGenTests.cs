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

internal static class AssignmentCodeGenTests
{
    public static Dictionary<string, Func<bool>> GetTests() => new()
    {
        { "Assign_BasicInt", TestAssignBasicInt },
        { "Assign_CompoundAdd", TestAssignCompoundAdd },
        { "Assign_CompoundSub", TestAssignCompoundSub },
        { "Assign_CompoundMul", TestAssignCompoundMul },
        { "Assign_CompoundDiv", TestAssignCompoundDiv },
        { "Assign_FloatCompound", TestAssignFloatCompound },
        { "Assign_AssignToConstThrows", TestAssignToConstThrows },
        { "Assign_AssignToUndefinedThrows", TestAssignToUndefinedThrows },
        { "Assign_IncompatibleTypeThrows", TestAssignIncompatibleTypeThrows },
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

    static bool TestAssignBasicInt()
    {
        string code = @"
void test() {
    int x = 5;
    x = 10;
}
";
        return Verify(code);
    }

    static bool TestAssignCompoundAdd()
    {
        string code = @"
void test() {
    int x = 10;
    x += 5;
}
";
        return Verify(code);
    }

    static bool TestAssignCompoundSub()
    {
        string code = @"
void test() {
    int x = 10;
    x -= 4;
}
";
        return Verify(code);
    }

    static bool TestAssignCompoundMul()
    {
        string code = @"
void test() {
    int x = 3;
    x *= 4;
}
";
        return Verify(code);
    }

    static bool TestAssignCompoundDiv()
    {
        string code = @"
void test() {
    int x = 20;
    x /= 5;
}
";
        return Verify(code);
    }

    static bool TestAssignFloatCompound()
    {
        string code = @"
void test() {
    float f = 2.5;
    f += 1.5;
    f -= 0.5;
    f *= 2.0;
    f /= 2.0;
}
";
        return Verify(code);
    }

    static bool TestAssignToConstThrows()
    {
        string code = @"
void test() {
    const int c = 42;
    c = 100;
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

    static bool TestAssignToUndefinedThrows()
    {
        string code = @"
void test() {
    missingVariable = 10;
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

    static bool TestAssignIncompatibleTypeThrows()
    {
        string code = @"
void test() {
    int x = 10;
    x = ""cannot assign string to int"";
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
}
