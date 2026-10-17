using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.Lexer;

namespace BifyLang.Tests
{
    internal static class SyntaxTests
    {
        public static Dictionary<string, Func<bool>> GetTests() => new()
        {
            // Variable declarations
            { "VarDecl_PrimitiveTypes", TestVarDecl_PrimitiveTypes },
            { "VarDecl_UnknownTypes", TestVarDecl_UnknownTypes },
            { "VarDecl_Uninitialized", TestVarDecl_Uninitialized },
            { "VarDecl_PointerTypes", TestVarDecl_PointerTypes },
            { "VarDecl_ArrayTypes", TestVarDecl_ArrayTypes },
            { "VarDecl_PointerAndArrayCombinations", TestVarDecl_PointerAndArrayCombinations },
            { "VarDecl_Modifiers", TestVarDecl_Modifiers },
            { "VarDecl_VarKeyword", TestVarDecl_VarKeyword },

            // Function declarations
            { "FuncDecl_PrimitiveReturn", TestFuncDecl_PrimitiveReturn },
            { "FuncDecl_UnknownReturn", TestFuncDecl_UnknownReturn },
            { "FuncDecl_VoidNoParams", TestFuncDecl_VoidNoParams },
            { "FuncDecl_PointerAndArrayReturn", TestFuncDecl_PointerAndArrayReturn },
            { "FuncDecl_Modifiers", TestFuncDecl_Modifiers },
            { "FuncDecl_MultipleParameters", TestFuncDecl_MultipleParameters },

            // Constructors & Classes
            { "Class_ConstructorsWithModifiers", TestClass_ConstructorsWithModifiers },
            { "Class_FieldsAndMethods", TestClass_FieldsAndMethods },

            // Statements & Control Flow
            { "ForLoop_UnknownTypeDecl", TestForLoop_UnknownTypeDecl },
            { "WhileAndIfStatements", TestWhileAndIfStatements },
            { "AssignmentsAndExpressions", TestAssignmentsAndExpressions },
            { "Switch_BasicCasesAndDefault", TestSwitch_BasicCasesAndDefault },
            { "Switch_BracedCases", TestSwitch_BracedCases },

            // Negative tests
            { "Negative_InvalidVariableName", TestNegative_InvalidVariableName },
            { "Negative_EmptyValueAssigned", TestNegative_EmptyValueAssigned },
            { "Negative_InvalidStatementBinaryOp", TestNegative_InvalidStatementBinaryOp },
            { "Negative_ClassMethodBeforeField", TestNegative_ClassMethodBeforeField },
            { "Negative_Switch_MissingExpression", TestNegative_Switch_MissingExpression },
            { "Negative_Switch_DuplicateDefault", TestNegative_Switch_DuplicateDefault },
            { "Negative_Switch_MissingColon", TestNegative_Switch_MissingColon },
        };

        private static AstNode Parse(string code)
        {
            MyLexer lexer = new(code);
            List<Token> tokens = lexer.Tokenize();
            string[] lines = code.Split('\n');
            AstTree tree = new(lines);
            return tree.ParseTokens(tokens);
        }

        private static bool ExpectError(string code)
        {
            try
            {
                Parse(code);
                return false;
            }
            catch
            {
                return true;
            }
        }

        
        static bool TestVarDecl_PrimitiveTypes()
        {
            string code = @"
void test() {
    int a = 5;
    float b = 3.14;
    string c = ""hello"";
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];
            if (fn.BlockNode.ChildNodes.Count != 3) return false;

            var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
            var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
            var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];

            return v1.Type is AstIdentifier id1 && id1.Name == "int" &&
                   v1.AssignmentNode.Left is AstIdentifier left1 && left1.Name == "a" &&
                   v1.AssignmentNode.Right is AstInteger n && n.Value.Equals(5) &&
                   v2.Type is AstIdentifier id2 && id2.Name == "float" &&
                   v3.Type is AstIdentifier id3 && id3.Name == "string";
        }

        static bool TestVarDecl_UnknownTypes()
        {
            string code = @"
void test() {
    MyCustomType x = new MyCustomType();
    Vector3 pos = getOrigin();
    UnregisteredType z = x;
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];
            if (fn.BlockNode.ChildNodes.Count != 3) return false;

            var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
            var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
            var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];

            return v1.Type is AstIdentifier id1 && id1.Name == "MyCustomType" &&
                   v1.AssignmentNode.Right is AstNew &&
                   v2.Type is AstIdentifier id2 && id2.Name == "Vector3" &&
                   v2.AssignmentNode.Right is AstCall &&
                   v3.Type is AstIdentifier id3 && id3.Name == "UnregisteredType";
        }

        static bool TestVarDecl_Uninitialized()
        {
            string code = @"
void test() {
    int a;
    CustomType b;
    CustomType* p;
    int[5] arr;
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];
            if (fn.BlockNode.ChildNodes.Count != 4) return false;

            for (int i = 0; i < 4; i++)
            {
                var v = (AstVarDecl)fn.BlockNode.ChildNodes[i];
                if (v.AssignmentNode.Right != null) return false;
            }

            var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
            var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
            var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];
            var v4 = (AstVarDecl)fn.BlockNode.ChildNodes[3];

            return v1.Type is AstIdentifier id1 && id1.Name == "int" &&
                   v2.Type is AstIdentifier id2 && id2.Name == "CustomType" &&
                   v3.Type is AstUnaryOperator uop && uop.Token.Type == TokenType.POINTER &&
                   v4.Type is AstIndexOperator idx && idx.IndexNode is AstInteger sz && sz.Value.Equals(5);
        }

        static bool TestVarDecl_PointerTypes()
        {
            string code = @"
void test() {
    int* p = null;
    Foo** pp;
    Bar*** ppp;
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];
            if (fn.BlockNode.ChildNodes.Count != 3) return false;

            var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
            var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
            var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];

            // int*
            if (v1.Type is not AstUnaryOperator ptr1 || ptr1.Operand is not AstIdentifier i1 || i1.Name != "int")
                return false;

            // Foo**
            if (v2.Type is not AstUnaryOperator ptr2 || ptr2.Operand is not AstUnaryOperator ptr2Inner ||
                ptr2Inner.Operand is not AstIdentifier i2 || i2.Name != "Foo")
                return false;

            // Bar***
            if (v3.Type is not AstUnaryOperator ptr3 || ptr3.Operand is not AstUnaryOperator ptr3Mid ||
                ptr3Mid.Operand is not AstUnaryOperator ptr3Inner || ptr3Inner.Operand is not AstIdentifier i3 || i3.Name != "Bar")
                return false;

            return true;
        }

        static bool TestVarDecl_ArrayTypes()
        {
            string code = @"
void test() {
    int[] a = [1, 2, 3];
    int[10] b;
    CustomClass[] c;
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];
            if (fn.BlockNode.ChildNodes.Count != 3) return false;

            var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
            var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
            var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];

            // int[]
            if (v1.Type is not AstIndexOperator arr1 || arr1.IndexNode != null ||
                arr1.TargetNode is not AstIdentifier t1 || t1.Name != "int")
                return false;

            // int[10]
            if (v2.Type is not AstIndexOperator arr2 || arr2.IndexNode is not AstInteger num || !num.Value.Equals(10) ||
                arr2.TargetNode is not AstIdentifier t2 || t2.Name != "int")
                return false;

            // CustomClass[]
            if (v3.Type is not AstIndexOperator arr3 || arr3.IndexNode != null ||
                arr3.TargetNode is not AstIdentifier t3 || t3.Name != "CustomClass")
                return false;

            return true;
        }

        static bool TestVarDecl_PointerAndArrayCombinations()
        {
            string code = @"
void test() {
    Foo*[3] ptrArr;
    int[][5] matrix;
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];
            if (fn.BlockNode.ChildNodes.Count != 2) return false;

            var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
            var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];

            // Foo*[3] -> Array of size 3 of Foo*
            if (v1.Type is not AstIndexOperator arr || arr.IndexNode is not AstInteger sz || !sz.Value.Equals(3) ||
                arr.TargetNode is not AstUnaryOperator ptr || ptr.Operand is not AstIdentifier id || id.Name != "Foo")
                return false;

            // int[][5] -> Array of size 5 of int[]
            if (v2.Type is not AstIndexOperator mtx || mtx.IndexNode is not AstInteger mtxSz || !mtxSz.Value.Equals(5) ||
                mtx.TargetNode is not AstIndexOperator innerArr || innerArr.TargetNode is not AstIdentifier mtxId || mtxId.Name != "int")
                return false;

            return true;
        }

        static bool TestVarDecl_Modifiers()
        {
            string code = @"
void test() {
    const int MAX = 100;
    public Foo instance = null;
    private const float PI = 3.14;
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];
            if (fn.BlockNode.ChildNodes.Count != 3) return false;

            var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
            var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
            var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];

            return v1.Flag.HasFlag("const") &&
                   v2.Flag.HasFlag("public") &&
                   v3.Flag.HasFlag("private") && v3.Flag.HasFlag("const");
        }

        static bool TestVarDecl_VarKeyword()
        {
            string code = @"
void test() {
    var x = 1;
    var y = x * 2;
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];
            if (fn.BlockNode.ChildNodes.Count != 2) return false;

            var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
            var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];

            return v1.Type is AstIdentifier id1 && id1.Name == "var" &&
                   v2.Type is AstIdentifier id2 && id2.Name == "var" &&
                   v2.AssignmentNode.Right is AstBinaryOp;
        }

        // ==========================================
        // Function Declaration Tests
        // ==========================================

        static bool TestFuncDecl_PrimitiveReturn()
        {
            string code = @"
int add(int a, int b) {
    return a + b;
}
";
            var module = (AstModule)Parse(code);
            if (module.ChildNodes.Count != 1) return false;

            var fn = (AstFunctionDecl)module.ChildNodes[0];
            return fn.FunctionNameNode.Name == "add" &&
                   fn.TypeNode is AstIdentifier tid && tid.Name == "int" &&
                   fn.BlockNode.ChildNodes.Count == 1 &&
                   fn.BlockNode.ChildNodes[0] is AstReturn;
        }

        static bool TestFuncDecl_UnknownReturn()
        {
            string code = @"
CustomResult process(Request req, Options opt) {
    return res;
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];
            return fn.FunctionNameNode.Name == "process" &&
                   fn.TypeNode is AstIdentifier tid && tid.Name == "CustomResult";
        }

        static bool TestFuncDecl_VoidNoParams()
        {
            string code = @"
void run() {
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];
            return fn.FunctionNameNode.Name == "run" &&
                   fn.TypeNode is AstIdentifier tid && tid.Name == "void" &&
                   fn.ArgumentsNode == null;
        }

        static bool TestFuncDecl_PointerAndArrayReturn()
        {
            string code = @"
Node* findNode(Tree* tree, int key) {
    return null;
}

int[] getBuffer() {
    return buf;
}
";
            var module = (AstModule)Parse(code);
            if (module.ChildNodes.Count != 2) return false;

            var fn1 = (AstFunctionDecl)module.ChildNodes[0];
            var fn2 = (AstFunctionDecl)module.ChildNodes[1];

            return fn1.TypeNode is AstUnaryOperator ptr && ptr.Token.Type == TokenType.POINTER &&
                   ptr.Operand is AstIdentifier id1 && id1.Name == "Node" &&
                   fn2.TypeNode is AstIndexOperator arr &&
                   arr.TargetNode is AstIdentifier id2 && id2.Name == "int";
        }

        static bool TestFuncDecl_Modifiers()
        {
            string code = @"
public void execute() {
}

private int calculate() {
    return 0;
}
";
            var module = (AstModule)Parse(code);
            if (module.ChildNodes.Count != 2) return false;

            var fn1 = (AstFunctionDecl)module.ChildNodes[0];
            var fn2 = (AstFunctionDecl)module.ChildNodes[1];

            return fn1.FlagNode.HasFlag("public") &&
                   fn2.FlagNode.HasFlag("private");
        }

        static bool TestFuncDecl_MultipleParameters()
        {
            string code = @"
void render(int width, Texture* tex, float[] coords, const int flags) {
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];

            return fn.FunctionNameNode.Name == "render" &&
                   fn.ArgumentsNode is AstBinaryOp;
        }

        // ==========================================
        // Constructor & Class Tests
        // ==========================================

        static bool TestClass_ConstructorsWithModifiers()
        {
            string code = @"
class Player {
    public constructor(int id) {
        this.id = id;
    }
    private constructor() {
    }
}
";
            var module = (AstModule)Parse(code);
            if (module.ChildNodes.Count != 1) return false;

            var cls = (AstClass)module.ChildNodes[0];
            var body = (AstBlock)cls.BodyNode;
            if (body.ChildNodes.Count != 2) return false;

            var ctor1 = (AstFunctionDecl)body.ChildNodes[0];
            var ctor2 = (AstFunctionDecl)body.ChildNodes[1];

            return ctor1.FunctionNameNode.Name == "constructor" &&
                   ctor1.TypeNode is AstIdentifier t1 && t1.Name == "void" &&
                   ctor1.FlagNode.HasFlag("public") &&
                   ctor2.FunctionNameNode.Name == "constructor" &&
                   ctor2.TypeNode is AstIdentifier t2 && t2.Name == "void" &&
                   ctor2.FlagNode.HasFlag("private");
        }

        static bool TestClass_FieldsAndMethods()
        {
            string code = @"
class Item {
    public int id = 0;
    CustomData data;

    public constructor(int id, CustomData d) {
        this.id = id;
        this.data = d;
    }

    public CustomData* getData() {
        return data;
    }
}
";
            var module = (AstModule)Parse(code);
            var cls = (AstClass)module.ChildNodes[0];
            var body = (AstBlock)cls.BodyNode;
            if (body.ChildNodes.Count != 4) return false;

            return body.ChildNodes[0] is AstVarDecl &&
                   body.ChildNodes[1] is AstVarDecl &&
                   body.ChildNodes[2] is AstFunctionDecl ctor && ctor.FunctionNameNode.Name == "constructor" &&
                   body.ChildNodes[3] is AstFunctionDecl method && method.FunctionNameNode.Name == "getData";
        }

        // ==========================================
        // Statements & Control Flow Tests
        // ==========================================

        static bool TestForLoop_UnknownTypeDecl()
        {
            string code = @"
void test() {
    for (CustomIterator* it = start; it != end; it++) {
        it.step();
    }
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];
            var forLoop = (AstFor)fn.BlockNode.ChildNodes[0];

            return forLoop.InitNode is AstVarDecl varDecl &&
                   varDecl.Type is AstUnaryOperator ptr &&
                   ptr.Operand is AstIdentifier id && id.Name == "CustomIterator" &&
                   forLoop.IncrementNode is AstUnaryOperator inc && inc.Token.Type == TokenType.INCREMENT;
        }

        static bool TestWhileAndIfStatements()
        {
            string code = @"
void test() {
    while (x > 0) {
        if (x == 1) {
            break;
        } else if (x == 2) {
            continue;
        } else {
            x = x - 1;
        }
    }
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];
            var whileLoop = (AstWhile)fn.BlockNode.ChildNodes[0];
            var whileBlock = (AstBlock)whileLoop.BlockNode;
            var ifStmt = (AstIf)whileBlock.ChildNodes[0];

            return ifStmt.ConditionNode is AstBinaryOp &&
                   ifStmt.ElseNode is AstElse;
        }

        static bool TestAssignmentsAndExpressions()
        {
            string code = @"
void test() {
    a = b * c;
    arr[0] = 42;
    this.field = value;
    foo(a, b);
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];
            if (fn.BlockNode.ChildNodes.Count != 4) return false;

            return fn.BlockNode.ChildNodes[0] is AstAssignmentOperator &&
                   fn.BlockNode.ChildNodes[1] is AstAssignmentOperator &&
                   fn.BlockNode.ChildNodes[2] is AstAssignmentOperator &&
                   fn.BlockNode.ChildNodes[3] is AstCall;
        }

        // ==========================================
        // Negative Tests (Invalid Syntax)
        // ==========================================

        static bool TestNegative_InvalidVariableName()
        {
            return ExpectError(@"
void test() {
    int 123 = 5;
}
");
        }

        static bool TestNegative_EmptyValueAssigned()
        {
            return ExpectError(@"
void test() {
    int a = ;
}
");
        }

        static bool TestNegative_InvalidStatementBinaryOp()
        {
            return ExpectError(@"
void test() {
    a + b;
}
");
        }

        static bool TestNegative_ClassMethodBeforeField()
        {
            return ExpectError(@"
class Bad {
    void method() {
    }
    int field = 1;
}
");
        }

        static bool TestSwitch_BasicCasesAndDefault()
        {
            string code = @"
void test(int x) {
    switch (x) {
        case 1:
            int a = 10;
            break;
        case 2:
            int b = 20;
            break;
        default:
            int c = 0;
            break;
    }
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];
            if (fn.BlockNode.ChildNodes[0] is not AstSwitch sw) return false;
            if (sw.ExpressionNode is not AstIdentifier id || id.Name != "x") return false;
            if (sw.Cases.Count != 2) return false;
            if (sw.Cases[0].ValueNode is not AstInteger n1 || !n1.Value.Equals(1)) return false;
            if (sw.Cases[0].BlockNode.ChildNodes.Count != 2) return false;
            if (sw.Cases[1].ValueNode is not AstInteger n2 || !n2.Value.Equals(2)) return false;
            if (sw.DefaultNode == null) return false;
            if (sw.DefaultNode.BlockNode.ChildNodes.Count != 2) return false;
            return true;
        }

        static bool TestSwitch_BracedCases()
        {
            string code = @"
void test(int x) {
    switch (x) {
        case 1: {
            int a = 10;
        }
        default: {
            int b = 0;
        }
    }
}
";
            var module = (AstModule)Parse(code);
            var fn = (AstFunctionDecl)module.ChildNodes[0];
            if (fn.BlockNode.ChildNodes[0] is not AstSwitch sw) return false;
            if (sw.Cases.Count != 1) return false;
            if (sw.DefaultNode == null) return false;
            return true;
        }

        static bool TestNegative_Switch_MissingExpression()
        {
            return ExpectError(@"
void test() {
    switch () {
        case 1:
            break;
    }
}
");
        }

        static bool TestNegative_Switch_DuplicateDefault()
        {
            return ExpectError(@"
void test(int x) {
    switch (x) {
        default:
            break;
        default:
            break;
    }
}
");
        }

        static bool TestNegative_Switch_MissingColon()
        {
            return ExpectError(@"
void test(int x) {
    switch (x) {
        case 1
            break;
    }
}
");
        }
    }
}
