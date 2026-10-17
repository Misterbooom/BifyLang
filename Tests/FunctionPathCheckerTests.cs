#nullable enable
using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Tests;

internal static class FunctionPathCheckerTests
{
    public static Dictionary<string, Func<bool>> GetTests() => new()
    {
        // Positive tests
        { "PathCheck_SimpleReturn", TestSimpleReturn },
        { "PathCheck_IfElseBothReturn", TestIfElseBothReturn },
        { "PathCheck_IfElseIfElseAllReturn", TestIfElseIfElseAllReturn },
        { "PathCheck_VoidNoReturn", TestVoidNoReturn },
        { "PathCheck_VoidEmptyReturn", TestVoidEmptyReturn },
        { "PathCheck_EarlyReturnDeadCodePruned", TestEarlyReturnDeadCodePruned },
        { "PathCheck_InfiniteWhileReturns", TestInfiniteWhileReturns },
        { "PathCheck_ConstructorVoidAllowed", TestConstructorVoidAllowed },
        { "PathCheck_SwitchAllReturn", TestSwitchAllReturn },

        // Negative tests
        { "PathCheck_MissingReturnNonVoid", TestMissingReturnNonVoid },
        { "PathCheck_IfWithoutElseFails", TestIfWithoutElseFails },
        { "PathCheck_IfElseBranchMissingReturn", TestIfElseBranchMissingReturn },
        { "PathCheck_EmptyReturnInNonVoid", TestEmptyReturnInNonVoid },
        { "PathCheck_WhileTrueWithBreakDoesNotGuaranteeReturn", TestWhileTrueWithBreakDoesNotGuaranteeReturn },
        { "PathCheck_SwitchMissingDefaultFails", TestSwitchMissingDefaultFails }
    };

    private static void ParseAndCheck(string code)
    {
        MyLexer lexer = new(code);
        List<Token> tokens = lexer.Tokenize();
        string[] lines = code.Split('\n');
        AstTree tree = new(lines);
        AstModule module = tree.ParseTokens(tokens);

        var ctx = new CodeGenContext();
        foreach (var child in module.ChildNodes)
        {
            if (child is AstFunctionDecl fn)
            {
                _ = new FunctionPathChecker(fn, ctx);
            }
            else if (child is AstClass cls)
            {
                if (cls.BodyNode is AstBlock block)
                {
                    foreach (var member in block.ChildNodes)
                    {
                        if (member is AstFunctionDecl method)
                        {
                            _ = new FunctionPathChecker(method, ctx);
                        }
                    }
                }
            }
        }
    }

    private static bool ExpectSuccess(string code)
    {
        try
        {
            ParseAndCheck(code);
            return Traceback.Instance.GetError() == null;
        }
        catch
        {
            return false;
        }
    }

    private static bool ExpectTypeError(string code, string? expectedSubstring = null)
    {
        Traceback.Instance.Catch(typeof(BifyTypeError));
        try
        {
            ParseAndCheck(code);
            var err = Traceback.Instance.GetError();
            if (err is BifyTypeError typeError)
            {
                if (expectedSubstring != null)
                {
                    return typeError.Message.Contains(expectedSubstring);
                }
                return true;
            }
            return false;
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

    // --- Positive Tests ---

    static bool TestSimpleReturn()
    {
        string code = @"
int getVal() {
    return 42;
}
";
        return ExpectSuccess(code);
    }

    static bool TestIfElseBothReturn()
    {
        string code = @"
int max(int a, int b) {
    if (a > b) {
        return a;
    } else {
        return b;
    }
}
";
        return ExpectSuccess(code);
    }

    static bool TestIfElseIfElseAllReturn()
    {
        string code = @"
int sign(int x) {
    if (x > 0) {
        return 1;
    } else if (x < 0) {
        return -1;
    } else {
        return 0;
    }
}
";
        return ExpectSuccess(code);
    }

    static bool TestVoidNoReturn()
    {
        string code = @"
void doWork() {
    int a = 1;
    int b = 2;
}
";
        return ExpectSuccess(code);
    }

    static bool TestVoidEmptyReturn()
    {
        string code = @"
void doWork(int a) {
    if (a > 0) {
        return;
    }
    int b = 2;
}
";
        return ExpectSuccess(code);
    }

    static bool TestEarlyReturnDeadCodePruned()
    {
        string code = @"
int compute() {
    return 100;
    int dead = 50;
}
";
        return ExpectSuccess(code);
    }

    static bool TestInfiniteWhileReturns()
    {
        string code = @"
int loopForever() {
    while (true) {
        return 1;
    }
}
";
        return ExpectSuccess(code);
    }

    static bool TestConstructorVoidAllowed()
    {
        string code = @"
class TestClass {
    int x;
    constructor(int val) {
        this.x = val;
        return;
    }
}
";
        return ExpectSuccess(code);
    }

    // --- Negative Tests ---

    static bool TestMissingReturnNonVoid()
    {
        string code = @"
int missing() {
    int a = 5;
}
";
        return ExpectTypeError(code, "Not all execution paths return a value for int");
    }

    static bool TestIfWithoutElseFails()
    {
        string code = @"
int check(int a) {
    if (a > 0) {
        return a;
    }
}
";
        return ExpectTypeError(code, "Not all execution paths return a value for int");
    }

    static bool TestIfElseBranchMissingReturn()
    {
        string code = @"
int check(int a) {
    if (a > 0) {
        int x = 1;
    } else {
        return 2;
    }
}
";
        return ExpectTypeError(code, "Not all execution paths return a value for int");
    }

    static bool TestEmptyReturnInNonVoid()
    {
        string code = @"
int getValue() {
    return;
}
";
        return ExpectTypeError(code, "Expected int but got void");
    }

    static bool TestWhileTrueWithBreakDoesNotGuaranteeReturn()
    {
        string code = @"
int loop() {
    while (true) {
        break;
    }
}
";
        return ExpectTypeError(code, "Not all execution paths return a value for int");
    }

    static bool TestSwitchAllReturn()
    {
        string code = @"
int getVal(int x) {
    switch (x) {
        case 1:
            return 10;
        case 2:
            return 20;
        default:
            return 0;
    }
}
";
        return ExpectSuccess(code);
    }

    static bool TestSwitchMissingDefaultFails()
    {
        string code = @"
int getVal(int x) {
    switch (x) {
        case 1:
            return 10;
    }
}
";
        return ExpectTypeError(code, "Not all execution paths return a value for int");
    }
}
