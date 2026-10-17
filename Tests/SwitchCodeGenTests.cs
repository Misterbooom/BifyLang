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

internal static class SwitchCodeGenTests
{
    public static Dictionary<string, Func<bool>> GetTests() => new()
    {
        { "Switch_Basic", TestSwitchBasic },
        { "Switch_MultipleCases", TestSwitchMultipleCases },
        { "Switch_DefaultCase", TestSwitchDefaultCase },
        { "Switch_WithoutDefault", TestSwitchWithoutDefault },
        { "Switch_WithBreak", TestSwitchWithBreak },
        { "Switch_AllBranchesReturn", TestSwitchAllBranchesReturn },
        { "Switch_InsideWhileLoop", TestSwitchInsideWhileLoop },
        { "Switch_CharExpression", TestSwitchCharExpression },
        { "Switch_FloatExpression", TestSwitchFloatExpression },
        { "Switch_DoubleExpression", TestSwitchDoubleExpression },
        { "Switch_FloatWithBreak", TestSwitchFloatWithBreak },
        { "Switch_FloatAllBranchesReturn", TestSwitchFloatAllBranchesReturn },
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

    static bool TestSwitchBasic()
    {
        string code = @"
void test(int x) {
    int y = 0;
    switch (x) {
        case 1: {
            y = 10;
        }
        default: {
            y = 20;
        }
    }
}
";
        return Verify(code);
    }

    static bool TestSwitchMultipleCases()
    {
        string code = @"
void test(int x) {
    int y = 0;
    switch (x) {
        case 1: {
            y = 10;
        }
        case 2: {
            y = 20;
        }
        case 3: {
            y = 30;
        }
        default: {
            y = 40;
        }
    }
}
";
        return Verify(code);
    }

    static bool TestSwitchDefaultCase()
    {
        string code = @"
void test(int x) {
    int y = 0;
    switch (x) {
        case 5: {
            y = 50;
        }
        default: {
            y = -1;
        }
    }
}
";
        return Verify(code);
    }

    static bool TestSwitchWithoutDefault()
    {
        string code = @"
void test(int x) {
    int y = 0;
    switch (x) {
        case 1: {
            y = 10;
        }
        case 2: {
            y = 20;
        }
    }
}
";
        return Verify(code);
    }

    static bool TestSwitchWithBreak()
    {
        string code = @"
void test(int x) {
    int y = 0;
    switch (x) {
        case 1: {
            y = 10;
            break;
        }
        default: {
            y = 20;
            break;
        }
    }
}
";
        return Verify(code);
    }

    static bool TestSwitchAllBranchesReturn()
    {
        string code = @"
int test(int x) {
    switch (x) {
        case 1: {
            return 10;
        }
        case 2: {
            return 20;
        }
        default: {
            return 30;
        }
    }
}
";
        return Verify(code);
    }

    static bool TestSwitchInsideWhileLoop()
    {
        string code = @"
void test() {
    int i = 0;
    while (i < 10) {
        switch (i) {
            case 5: {
                break;
            }
        }
        i++;
    }
}
";
        return Verify(code);
    }

    static bool TestSwitchCharExpression()
    {
        string code = @"
void test(char c) {
    int val = 0;
    switch (c) {
        case 65: {
            val = 1;
        }
        default: {
            val = 2;
        }
    }
}
";
        return Verify(code);
    }

    static bool TestSwitchFloatExpression()
    {
        string code = @"
void test(float x) {
    int y = 0;
    switch (x) {
        case 1.5: {
            y = 1;
        }
        case 2.5: {
            y = 2;
        }
        default: {
            y = 3;
        }
    }
}
";
        return Verify(code);
    }

    static bool TestSwitchDoubleExpression()
    {
        string code = @"
void test(double d) {
    int y = 0;
    switch (d) {
        case 3.14: {
            y = 314;
        }
        default: {
            y = 0;
        }
    }
}
";
        return Verify(code);
    }

    static bool TestSwitchFloatWithBreak()
    {
        string code = @"
void test(float x) {
    int y = 0;
    switch (x) {
        case 1.0: {
            y = 1;
            break;
        }
        default: {
            y = 2;
            break;
        }
    }
}
";
        return Verify(code);
    }

    static bool TestSwitchFloatAllBranchesReturn()
    {
        string code = @"
int test(float x) {
    switch (x) {
        case 1.0: {
            return 1;
        }
        case 2.0: {
            return 2;
        }
        default: {
            return 3;
        }
    }
}
";
        return Verify(code);
    }
}
