using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using BifyLang.Ast;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Tests
{
    internal class Tests
    {
        public static void RunTests()
        {
            var testResults = new Dictionary<string, Func<bool>>(SyntaxTests.GetTests());
            foreach (var kvp in FunctionPathCheckerTests.GetTests())
            {
                testResults[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in FunctionCodeGenTests.GetTests())
            {
                testResults[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in ExpressionCodeGenTests.GetTests())
            {
                testResults[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in VariableDeclCodeGenTests.GetTests())
            {
                testResults[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in AssignmentCodeGenTests.GetTests())
            {
                testResults[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in UnaryCodeGenTests.GetTests())
            {
                testResults[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in CallCodeGenTests.GetTests())
            {
                testResults[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in ControlFlowCodeGenTests.GetTests())
            {
                testResults[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in SwitchCodeGenTests.GetTests())
            {
                testResults[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in ClassCodeGenTests.GetTests())
            {
                testResults[kvp.Key] = kvp.Value;
            }
            testResults["AstVerificationTest"] = AstVerificationTest;
            testResults["PascalTriangleTest"] = PascalTriangleTest;
            testResults["CountToOneMillionTest"] = CountToOneMillionTest;

            Console.WriteLine("\nTest Results:");

            Stopwatch totalStopwatch = new(); 
            totalStopwatch.Start();

            int passedCount = 0;

            foreach (var result in testResults)
            {
                try
                {
                    Stopwatch testStopwatch = new(); 
                    testStopwatch.Start();

                    bool passed = result.Value();
                    Console.WriteLine($"{result.Key}: {(passed ? "Passed" : "Failed")}");
                    
                    testStopwatch.Stop();
                    Console.WriteLine($"Test Execution Time: {testStopwatch.ElapsedMilliseconds} ms");
                    if (passed)
                    {
                        passedCount++;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{result.Key}: Failed");
                    Console.WriteLine($"Line: {Traceback.Instance.Line}");
                    Console.WriteLine(e.Message);
                    throw;
                }
            }

            totalStopwatch.Stop();
            Console.WriteLine($"\nTotal Execution Time: {totalStopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Total Passed: {passedCount}/{testResults.Count}");
        }
        static bool AstVerificationTest()
        {
            string testCode = @"
class Entity {
    public int id = 1;
    UnknownType data;
    public constructor(int id, UnknownType* ptr) {
        this.id = id;
    }
    public UnknownType* getData() {
        return data;
    }
}

int calculate(int x, CustomClass* ptr, int[] list) {
    int localVal = 42;
    CustomClass obj;
    CustomClass* p = ptr;
    int** dblPtr;
    int[10] buffer;
    CustomClass*[5] classPtrs;
    const int limit = 100;
    var product = x * localVal;

    for (CustomClass* it = p; it != null; it++) {
        localVal = localVal + 1;
    }

    localVal = x * limit;
    buffer[0] = localVal;
    return localVal;
}

void main() {
    Entity ent = new Entity(1, null);
}
";

            MyLexer lexer = new(testCode);
            var tokens = lexer.Tokenize();
            var lines = testCode.Split('\n');
            AstTree tree = new(lines);
            AstModule module = tree.ParseTokens(tokens);

            if (module.ChildNodes.Count != 3)
                return false;

            if (module.ChildNodes[0] is not AstClass cls)
                return false;

            if (cls.BodyNode is not AstBlock clsBody || clsBody.ChildNodes.Count != 4)
                return false;

            if (module.ChildNodes[1] is not AstFunctionDecl fn || fn.BlockNode.ChildNodes.Count != 12)
                return false;

            if (module.ChildNodes[2] is not AstFunctionDecl)
                return false;

            return true;
        }

        static bool CountToOneMillionTest()
        {
            var code = @"
                for (var i = 0; i < 10**6;i++){}
";
            return RunCode(code);
        }

        static bool PascalTriangleTest()
        {
            var code = @"
    int pascal(int row, int col) {
        var result = 1;
        var i = 1;
        while (i <= col) {
            result = result * (row - i + 1) / i;
            i = i + 1;
        }
        return result;
    }

    var rows = 12;
    var i = 0;
    while (i < rows) {
        var j = 0;
        var Line = """";
        while (j <= i) {
            Line = Line + parse(pascal(i, j), ""string"") + "" "";
            j = j + 1;
        }
        explode(Line);
        i = i + 1;
    }
    ";

            return RunCode(code);
        }

        static bool RunCode(string code)
        {
            Console.OutputEncoding = Encoding.UTF8;

            Stopwatch compilationStopwatch = new();
            compilationStopwatch.Start();

            var lexer = new MyLexer(code);
            var tokens = lexer.Tokenize();
            var codeByLine = code.Split('\n');
            var astParser = new AstTree(codeByLine);
            var node = astParser.ParseTokens(tokens);

            compilationStopwatch.Stop();
            Console.WriteLine($"Compilation Time: {compilationStopwatch.ElapsedMilliseconds} ms");

            Stopwatch executionStopwatch = new(); 
            executionStopwatch.Start();


            executionStopwatch.Stop();
            Console.WriteLine($"Execution Time: {executionStopwatch.ElapsedMilliseconds} ms");

            return true;
        }
    }
}
