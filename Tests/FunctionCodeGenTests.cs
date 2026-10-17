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

internal static class FunctionCodeGenTests
{
    public static Dictionary<string, Func<bool>> GetTests() => new()
    {
        { "CodeGen_VoidFunctionNoParams", TestVoidFunctionNoParams },
        { "CodeGen_VoidFunctionWithPrimitiveParams", TestVoidFunctionWithPrimitiveParams },
        { "CodeGen_VoidFunctionWithPointersAndArrays", TestVoidFunctionWithPointersAndArrays },
        { "CodeGen_MultipleFunctionsInOneModule", TestMultipleFunctionsInOneModule },
        { "CodeGen_FunctionParameterOrderPreserved", TestFunctionParameterOrderPreserved },
        { "CodeGen_DuplicateParameterNameThrows", TestDuplicateParameterNameThrows },
        { "CodeGen_NonVoidWithoutReturnThrows", TestNonVoidWithoutReturnThrows },
    };

    private static (LLVMModuleRef Module, CodeGenContext Context) CompileToModule(string code)
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

    static bool TestVoidFunctionNoParams()
    {
        string code = "void testFunc() {}";
        var (module, ctx) = CompileToModule(code);
        using (ctx)
        {
            if (!BifyCompiler.VerifyModule(module, out string? error))
            {
                Console.WriteLine($"VerifyModule failed: {error}");
                return false;
            }

            LLVMValueRef func = module.GetNamedFunction("testFunc");
            if (func.Handle == IntPtr.Zero)
                return false;

            if (func.ParamsCount != 0)
                return false;

            LLVMBasicBlockRef entry = func.FirstBasicBlock;
            if (entry.Handle == IntPtr.Zero)
                return false;

            return true;
        }
    }

    static bool TestVoidFunctionWithPrimitiveParams()
    {
        string code = "void compute(int a, float b, bool c) {}";
        var (module, ctx) = CompileToModule(code);
        using (ctx)
        {
            if (!BifyCompiler.VerifyModule(module, out string? error))
            {
                Console.WriteLine($"VerifyModule failed: {error}");
                return false;
            }

            LLVMValueRef func = module.GetNamedFunction("compute");
            if (func.Handle == IntPtr.Zero)
                return false;

            if (func.ParamsCount != 3)
                return false;

            LLVMValueRef paramA = func.GetParam(0);
            LLVMValueRef paramB = func.GetParam(1);
            LLVMValueRef paramC = func.GetParam(2);

            if (paramA.TypeOf.Kind != LLVMTypeKind.LLVMIntegerTypeKind || paramA.TypeOf.IntWidth != 32)
                return false;

            if (paramB.TypeOf.Kind != LLVMTypeKind.LLVMFloatTypeKind)
                return false;

            if (paramC.TypeOf.Kind != LLVMTypeKind.LLVMIntegerTypeKind || paramC.TypeOf.IntWidth != 1)
                return false;

            return true;
        }
    }

    static bool TestVoidFunctionWithPointersAndArrays()
    {
        string code = "void processData(int* ptr, float[] list, CustomType* obj) {}";
        var (module, ctx) = CompileToModule(code);
        using (ctx)
        {
            if (!BifyCompiler.VerifyModule(module, out string? error))
            {
                Console.WriteLine($"VerifyModule failed: {error}");
                return false;
            }

            LLVMValueRef func = module.GetNamedFunction("processData");
            if (func.Handle == IntPtr.Zero)
                return false;

            if (func.ParamsCount != 3)
                return false;

            if (func.GetParam(0).TypeOf.Kind != LLVMTypeKind.LLVMPointerTypeKind)
                return false;

            if (func.GetParam(1).TypeOf.Kind != LLVMTypeKind.LLVMPointerTypeKind)
                return false;

            if (func.GetParam(2).TypeOf.Kind != LLVMTypeKind.LLVMPointerTypeKind)
                return false;

            return true;
        }
    }

    static bool TestMultipleFunctionsInOneModule()
    {
        string code = @"
void init() {}
void update(float dt) {}
void render(int* buffer, int width, int height) {}
";
        var (module, ctx) = CompileToModule(code);
        using (ctx)
        {
            if (!BifyCompiler.VerifyModule(module, out string? error))
            {
                Console.WriteLine($"VerifyModule failed: {error}");
                return false;
            }

            LLVMValueRef initFunc = module.GetNamedFunction("init");
            LLVMValueRef updateFunc = module.GetNamedFunction("update");
            LLVMValueRef renderFunc = module.GetNamedFunction("render");

            if (initFunc.Handle == IntPtr.Zero || initFunc.ParamsCount != 0)
                return false;

            if (updateFunc.Handle == IntPtr.Zero || updateFunc.ParamsCount != 1)
                return false;

            if (renderFunc.Handle == IntPtr.Zero || renderFunc.ParamsCount != 3)
                return false;

            return true;
        }
    }

    static bool TestFunctionParameterOrderPreserved()
    {
        string code = "void checkOrder(int first, float second, bool third) {}";
        var (module, ctx) = CompileToModule(code);
        using (ctx)
        {
            if (!BifyCompiler.VerifyModule(module, out string? error))
            {
                Console.WriteLine($"VerifyModule failed: {error}");
                return false;
            }

            LLVMValueRef func = module.GetNamedFunction("checkOrder");
            if (func.Handle == IntPtr.Zero || func.ParamsCount != 3)
                return false;

            // Parameter 0 must be int (i32)
            if (func.GetParam(0).TypeOf.Kind != LLVMTypeKind.LLVMIntegerTypeKind || func.GetParam(0).TypeOf.IntWidth != 32)
                return false;

            // Parameter 1 must be float
            if (func.GetParam(1).TypeOf.Kind != LLVMTypeKind.LLVMFloatTypeKind)
                return false;

            // Parameter 2 must be bool (i1)
            if (func.GetParam(2).TypeOf.Kind != LLVMTypeKind.LLVMIntegerTypeKind || func.GetParam(2).TypeOf.IntWidth != 1)
                return false;

            return true;
        }
    }

    static bool TestDuplicateParameterNameThrows()
    {
        string code = "void badFunc(int x, float x) {}";
        Traceback.Instance.Catch(typeof(BifyNameError));
        try
        {
            var (_, ctx) = CompileToModule(code);
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

    static bool TestNonVoidWithoutReturnThrows()
    {
        string code = "int badFunc() {}";
        Traceback.Instance.Catch(typeof(BifyTypeError));
        try
        {
            var (_, ctx) = CompileToModule(code);
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
