#nullable enable
using BifyLang.Ast;
using BifyLang.Lexer;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.Syntax;

[TestFixture]
public class SyntaxTests : BifyTestBase
{
    private static void AssertIdentifier(AstNode? node, string expectedName)
    {
        Assert.That(node, Is.InstanceOf<AstIdentifier>());
        Assert.That(((AstIdentifier)node!).Name, Is.EqualTo(expectedName));
    }

    private static void AssertInteger(AstNode? node, int expectedValue)
    {
        Assert.That(node, Is.InstanceOf<AstInteger>());
        Assert.That(((AstInteger)node!).Value, Is.EqualTo(expectedValue));
    }

    [Test]
    public void VarDecl_PrimitiveTypes()
    {
        string code = @"
void test() {
    int a = 5;
    float b = 3.14;
    string c = ""hello"";
}
";
        var module = Bify.Parse(code);
        Assert.That(module.ChildNodes.Count, Is.GreaterThanOrEqualTo(1));
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(3));

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
        var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];

        AssertIdentifier(v1.Type, "int");
        AssertIdentifier(v1.AssignmentNode.Left, "a");
        AssertInteger(v1.AssignmentNode.Right, 5);
        AssertIdentifier(v2.Type, "float");
        AssertIdentifier(v3.Type, "string");
    }

    [Test]
    public void VarDecl_UnknownTypes()
    {
        string code = @"
void test() {
    MyCustomType x = new MyCustomType();
    Vector3 pos = getOrigin();
    UnregisteredType z = x;
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(3));

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
        var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];

        AssertIdentifier(v1.Type, "MyCustomType");
        Assert.That(v1.AssignmentNode.Right, Is.InstanceOf<AstNew>());
        AssertIdentifier(v2.Type, "Vector3");
        Assert.That(v2.AssignmentNode.Right, Is.InstanceOf<AstCall>());
        AssertIdentifier(v3.Type, "UnregisteredType");
    }

    [Test]
    public void VarDecl_Uninitialized()
    {
        string code = @"
void test() {
    int a;
    CustomType b;
    CustomType* p;
    int[5] arr;
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(4));

        for (int i = 0; i < 4; i++)
        {
            var v = (AstVarDecl)fn.BlockNode.ChildNodes[i];
            Assert.That(v.AssignmentNode.Right, Is.Null);
        }

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
        var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];
        var v4 = (AstVarDecl)fn.BlockNode.ChildNodes[3];

        AssertIdentifier(v1.Type, "int");
        AssertIdentifier(v2.Type, "CustomType");
        Assert.That(v3.Type, Is.InstanceOf<AstUnaryOperator>());
        var uop = (AstUnaryOperator)v3.Type;
        Assert.That(uop.Token.Type, Is.EqualTo(TokenType.POINTER));
        Assert.That(v4.Type, Is.InstanceOf<AstIndexOperator>());
        var idx = (AstIndexOperator)v4.Type;
        AssertInteger(idx.IndexNode, 5);
    }

    [Test]
    public void VarDecl_PointerTypes()
    {
        string code = @"
void test() {
    int* p = null;
    Foo** pp;
    Bar*** ppp;
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(3));

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
        var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];

        Assert.That(v1.Type, Is.InstanceOf<AstUnaryOperator>());
        AssertIdentifier(((AstUnaryOperator)v1.Type).Operand, "int");

        Assert.That(v2.Type, Is.InstanceOf<AstUnaryOperator>());
        var ptr2 = (AstUnaryOperator)v2.Type;
        Assert.That(ptr2.Operand, Is.InstanceOf<AstUnaryOperator>());
        AssertIdentifier(((AstUnaryOperator)ptr2.Operand).Operand, "Foo");

        Assert.That(v3.Type, Is.InstanceOf<AstUnaryOperator>());
    }

    [Test]
    public void VarDecl_ArrayTypes()
    {
        string code = @"
void test() {
    int[] a = [1, 2, 3];
    int[10] b;
    CustomClass[] c;
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(3));

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
        var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];

        var arr1 = (AstIndexOperator)v1.Type;
        Assert.That(arr1.IndexNode, Is.Null);
        AssertIdentifier(arr1.TargetNode, "int");

        var arr2 = (AstIndexOperator)v2.Type;
        AssertInteger(arr2.IndexNode, 10);
        AssertIdentifier(arr2.TargetNode, "int");

        var arr3 = (AstIndexOperator)v3.Type;
        AssertIdentifier(arr3.TargetNode, "CustomClass");
    }

    [Test]
    public void VarDecl_PointerAndArrayCombinations()
    {
        string code = @"
void test() {
    Foo*[3] ptrArr;
    int[][5] matrix;
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(2));

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];

        var arr = (AstIndexOperator)v1.Type;
        AssertInteger(arr.IndexNode, 3);
        Assert.That(arr.TargetNode, Is.InstanceOf<AstUnaryOperator>());

        var mtx = (AstIndexOperator)v2.Type;
        AssertInteger(mtx.IndexNode, 5);
        Assert.That(mtx.TargetNode, Is.InstanceOf<AstIndexOperator>());
    }

    [Test]
    public void VarDecl_Modifiers()
    {
        string code = @"
void test() {
    const int MAX = 100;
    public Foo instance = null;
    private const float PI = 3.14;
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(3));

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
        var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];

        Assert.That(v1.Flag.HasFlag("const"), Is.True);
        Assert.That(v2.Flag.HasFlag("public"), Is.True);
        Assert.That(v3.Flag.HasFlag("private") && v3.Flag.HasFlag("const"), Is.True);
    }

    [Test]
    public void VarDecl_VarKeyword()
    {
        string code = @"
void test() {
    var x = 1;
    var y = x * 2;
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(2));

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];

        AssertIdentifier(v1.Type, "var");
        AssertIdentifier(v2.Type, "var");
        Assert.That(v2.AssignmentNode.Right, Is.InstanceOf<AstBinaryOp>());
    }

    [Test]
    public void FuncDecl_PrimitiveReturn()
    {
        string code = @"
int add(int a, int b) {
    return a + b;
}
";
        var module = Bify.Parse(code);
        Assert.That(module.ChildNodes.Count, Is.EqualTo(1));

        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.FunctionNameNode.Name, Is.EqualTo("add"));
        AssertIdentifier(fn.TypeNode, "int");
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(1));
        Assert.That(fn.BlockNode.ChildNodes[0], Is.InstanceOf<AstReturn>());
    }

    [Test]
    public void FuncDecl_UnknownReturn()
    {
        string code = @"
CustomResult process(Request req, Options opt) {
    return res;
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.FunctionNameNode.Name, Is.EqualTo("process"));
        AssertIdentifier(fn.TypeNode, "CustomResult");
    }

    [Test]
    public void FuncDecl_VoidNoParams()
    {
        string code = @"
void run() {
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.FunctionNameNode.Name, Is.EqualTo("run"));
        AssertIdentifier(fn.TypeNode, "void");
        Assert.That(fn.ArgumentsNode, Is.Null);
    }

    [Test]
    public void FuncDecl_PointerAndArrayReturn()
    {
        string code = @"
Node* findNode(Tree* tree, int key) {
    return null;
}

int[] getBuffer() {
    return buf;
}
";
        var module = Bify.Parse(code);
        Assert.That(module.ChildNodes.Count, Is.EqualTo(2));

        var fn1 = (AstFunctionDecl)module.ChildNodes[0];
        var fn2 = (AstFunctionDecl)module.ChildNodes[1];

        Assert.That(fn1.TypeNode, Is.InstanceOf<AstUnaryOperator>());
        var ptr = (AstUnaryOperator)fn1.TypeNode;
        Assert.That(ptr.Token.Type, Is.EqualTo(TokenType.POINTER));
        AssertIdentifier(ptr.Operand, "Node");

        Assert.That(fn2.TypeNode, Is.InstanceOf<AstIndexOperator>());
        var arr = (AstIndexOperator)fn2.TypeNode;
        AssertIdentifier(arr.TargetNode, "int");
    }

    [Test]
    public void FuncDecl_Modifiers()
    {
        string code = @"
public void execute() {
}

private int calculate() {
    return 0;
}
";
        var module = Bify.Parse(code);
        Assert.That(module.ChildNodes.Count, Is.EqualTo(2));

        var fn1 = (AstFunctionDecl)module.ChildNodes[0];
        var fn2 = (AstFunctionDecl)module.ChildNodes[1];

        Assert.That(fn1.FlagNode.HasFlag("public"), Is.True);
        Assert.That(fn2.FlagNode.HasFlag("private"), Is.True);
    }

    [Test]
    public void FuncDecl_MultipleParameters()
    {
        string code = @"
void render(int width, Texture* tex, float[] coords, const int flags) {
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];

        Assert.That(fn.FunctionNameNode.Name, Is.EqualTo("render"));
        Assert.That(fn.ArgumentsNode, Is.InstanceOf<AstBinaryOp>());
    }

    [Test]
    public void Class_ConstructorsWithModifiers()
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
        var module = Bify.Parse(code);
        Assert.That(module.ChildNodes.Count, Is.EqualTo(1));

        var cls = (AstClass)module.ChildNodes[0];
        var body = (AstBlock)cls.BodyNode;
        Assert.That(body.ChildNodes.Count, Is.EqualTo(2));

        var ctor1 = (AstFunctionDecl)body.ChildNodes[0];
        var ctor2 = (AstFunctionDecl)body.ChildNodes[1];

        Assert.That(ctor1.FunctionNameNode.Name, Is.EqualTo("constructor"));
        AssertIdentifier(ctor1.TypeNode, "void");
        Assert.That(ctor1.FlagNode.HasFlag("public"), Is.True);
        Assert.That(ctor2.FunctionNameNode.Name, Is.EqualTo("constructor"));
        AssertIdentifier(ctor2.TypeNode, "void");
        Assert.That(ctor2.FlagNode.HasFlag("private"), Is.True);
    }

    [Test]
    public void Class_FieldsAndMethods()
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
        var module = Bify.Parse(code);
        var cls = (AstClass)module.ChildNodes[0];
        var body = (AstBlock)cls.BodyNode;
        Assert.That(body.ChildNodes.Count, Is.EqualTo(4));

        Assert.That(body.ChildNodes[0], Is.InstanceOf<AstVarDecl>());
        Assert.That(body.ChildNodes[1], Is.InstanceOf<AstVarDecl>());
        Assert.That(body.ChildNodes[2], Is.InstanceOf<AstFunctionDecl>());
        Assert.That(((AstFunctionDecl)body.ChildNodes[2]).FunctionNameNode.Name, Is.EqualTo("constructor"));
        Assert.That(body.ChildNodes[3], Is.InstanceOf<AstFunctionDecl>());
        Assert.That(((AstFunctionDecl)body.ChildNodes[3]).FunctionNameNode.Name, Is.EqualTo("getData"));
    }

    [Test]
    public void ForLoop_UnknownTypeDecl()
    {
        string code = @"
void test() {
    for (CustomIterator* it = start; it != end; it++) {
        it.step();
    }
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        var forLoop = (AstFor)fn.BlockNode.ChildNodes[0];

        Assert.That(forLoop.InitNode, Is.InstanceOf<AstVarDecl>());
        var varDecl = (AstVarDecl)forLoop.InitNode;
        Assert.That(varDecl.Type, Is.InstanceOf<AstUnaryOperator>());
        var ptr = (AstUnaryOperator)varDecl.Type;
        AssertIdentifier(ptr.Operand, "CustomIterator");
        Assert.That(forLoop.IncrementNode, Is.InstanceOf<AstUnaryOperator>());
        var inc = (AstUnaryOperator)forLoop.IncrementNode;
        Assert.That(inc.Token.Type, Is.EqualTo(TokenType.INCREMENT));
    }

    [Test]
    public void WhileAndIfStatements()
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
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        var whileLoop = (AstWhile)fn.BlockNode.ChildNodes[0];
        var whileBlock = (AstBlock)whileLoop.BlockNode;
        var ifStmt = (AstIf)whileBlock.ChildNodes[0];

        Assert.That(ifStmt.ConditionNode, Is.InstanceOf<AstBinaryOp>());
        Assert.That(ifStmt.ElseNode, Is.InstanceOf<AstElse>());
    }

    [Test]
    public void AssignmentsAndExpressions()
    {
        string code = @"
void test() {
    a = b * c;
    arr[0] = 42;
    this.field = value;
    foo(a, b);
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(4));

        Assert.That(fn.BlockNode.ChildNodes[0], Is.InstanceOf<AstAssignmentOperator>());
        Assert.That(fn.BlockNode.ChildNodes[1], Is.InstanceOf<AstAssignmentOperator>());
        Assert.That(fn.BlockNode.ChildNodes[2], Is.InstanceOf<AstAssignmentOperator>());
        Assert.That(fn.BlockNode.ChildNodes[3], Is.InstanceOf<AstCall>());
    }

    [Test]
    public void Switch_BasicCasesAndDefault()
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
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes[0], Is.InstanceOf<AstSwitch>());
        var sw = (AstSwitch)fn.BlockNode.ChildNodes[0];
        AssertIdentifier(sw.ExpressionNode, "x");
        Assert.That(sw.Cases.Count, Is.EqualTo(2));
        AssertInteger(sw.Cases[0].ValueNode, 1);
        Assert.That(sw.Cases[0].BlockNode.ChildNodes.Count, Is.EqualTo(2));
        AssertInteger(sw.Cases[1].ValueNode, 2);
        Assert.That(sw.DefaultNode, Is.Not.Null);
        Assert.That(sw.DefaultNode!.BlockNode.ChildNodes.Count, Is.EqualTo(2));
    }

    [Test]
    public void Switch_BracedCases()
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
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes[0], Is.InstanceOf<AstSwitch>());
        var sw = (AstSwitch)fn.BlockNode.ChildNodes[0];
        Assert.That(sw.Cases.Count, Is.EqualTo(1));
        Assert.That(sw.DefaultNode, Is.Not.Null);
    }

    [Test]
    public void Negative_InvalidVariableName()
    {
        BifyAssert.AnySyntaxError(@"
void test() {
    int 123 = 5;
}
");
    }

    [Test]
    public void Negative_EmptyValueAssigned()
    {
        BifyAssert.AnySyntaxError(@"
void test() {
    int a = ;
}
");
    }

    [Test]
    public void Negative_InvalidStatementBinaryOp()
    {
        BifyAssert.AnySyntaxError(@"
void test() {
    a + b;
}
");
    }

    [Test]
    public void Negative_ClassMethodBeforeField()
    {
        BifyAssert.AnySyntaxError(@"
class Bad {
    void method() {
    }
    int field = 1;
}
");
    }

    [Test]
    public void Negative_Switch_MissingExpression()
    {
        BifyAssert.AnySyntaxError(@"
void test() {
    switch () {
        case 1:
            break;
    }
}
");
    }

    [Test]
    public void Negative_Switch_DuplicateDefault()
    {
        BifyAssert.AnySyntaxError(@"
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

    [Test]
    public void Negative_Switch_MissingColon()
    {
        BifyAssert.AnySyntaxError(@"
void test(int x) {
    switch (x) {
        case 1
            break;
    }
}
");
    }
}
