#nullable enable
using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen;
using BifyLang.CodeGen.Core;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.Tests;

internal static class ControlFlowCodeGenTests
{
    public static Dictionary<string, Func<bool>> GetTests() => new()
    {
        { "ControlFlow_IfSimple", TestIfSimple },
        { "ControlFlow_IfElse", TestIfElse },
        { "ControlFlow_IfElseIfElse", TestIfElseIfElse },
        { "ControlFlow_IfBothBranchesReturn", TestIfBothBranchesReturn },
        { "ControlFlow_IfElseIfAllBranchesReturn", TestIfElseIfAllBranchesReturn },
        { "ControlFlow_WhileSimple", TestWhileSimple },
        { "ControlFlow_WhileWithConditionAndReturn", TestWhileWithConditionAndReturn },
        { "ControlFlow_ForSimple", TestForSimple },
        { "ControlFlow_ForSum", TestForSum },
        { "ControlFlow_ForTwoConsecutiveLoops", TestForTwoConsecutiveLoops },
        { "ControlFlow_ForEarlyReturn", TestForEarlyReturn },
        { "ControlFlow_ForBreak", TestForBreak },
        { "ControlFlow_ForContinue", TestForContinue },
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

    static bool TestIfSimple()
    {
        string code = @"
void test(int x) {
    int y = 0;
    if (x > 0) {
        y = 1;
    }
}
";
        return Verify(code);
    }

    static bool TestIfElse()
    {
        string code = @"
void test(int x) {
    int y = 0;
    if (x > 0) {
        y = 1;
    } else {
        y = 2;
    }
}
";
        return Verify(code);
    }

    static bool TestIfElseIfElse()
    {
        string code = @"
void test(int x) {
    int y = 0;
    if (x > 10) {
        y = 1;
    } else if (x > 5) {
        y = 2;
    } else if (x > 0) {
        y = 3;
    } else {
        y = 4;
    }
}
";
        return Verify(code);
    }

    static bool TestIfBothBranchesReturn()
    {
        string code = @"
int test(int x) {
    if (x > 0) {
        return 1;
    } else {
        return 2;
    }
}
";
        return Verify(code);
    }

    static bool TestIfElseIfAllBranchesReturn()
    {
        string code = @"
int test(int x) {
    if (x > 10) {
        return 1;
    } else if (x > 5) {
        return 2;
    } else {
        return 3;
    }
}
";
        return Verify(code);
    }

    static bool TestWhileSimple()
    {
        string code = @"
void test() {
    int i = 0;
    while (i < 10) {
        i++;
    }
}
";
        return Verify(code);
    }

    static bool TestWhileWithConditionAndReturn()
    {
        string code = @"
int test(int n) {
    int sum = 0;
    int i = 1;
    while (i <= n) {
        sum += i;
        i++;
    }
    return sum;
}
";
        return Verify(code);
    }

    static bool TestForSimple()
    {
        string code = @"
void test() {
    for (int i = 0; i < 10; i++) {
    }
}
";
        return Verify(code);
    }

    static bool TestForSum()
    {
        string code = @"
int test(int n) {
    int sum = 0;
    for (int i = 1; i <= n; i++) {
        sum += i;
    }
    return sum;
}
";
        return Verify(code);
    }

    static bool TestForTwoConsecutiveLoops()
    {
        string code = @"
void test() {
    for (int i = 0; i < 5; i++) {
    }
    for (int i = 0; i < 5; i++) {
    }
}
";
        return Verify(code);
    }

    static bool TestForEarlyReturn()
    {
        string code = @"
int test() {
    for (int i = 0; i < 10; i++) {
        return i;
    }
    return 0;
}
";
        return Verify(code);
    }

    static bool TestForBreak()
    {
        string code = @"
void test() {
    for (int i = 0; i < 10; i++) {
        break;
    }
}
";
        return Verify(code);
    }

    static bool TestForContinue()
    {
        string code = @"
void test() {
    for (int i = 0; i < 10; i++) {
        continue;
    }
}
";
        return Verify(code);
    }
}
