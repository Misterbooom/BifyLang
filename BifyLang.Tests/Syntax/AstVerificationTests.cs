#nullable enable
using BifyLang.Ast;
using BifyLang.Tests.Infrastructure;
using NUnit.Framework;

namespace BifyLang.Tests.Syntax;

[TestFixture]
public class AstVerificationTests : BifyTestBase
{
    [Test]
    public void AstVerificationTest()
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

        var module = Bify.Parse(testCode);

        Assert.That(module.ChildNodes.Count, Is.EqualTo(3));
        Assert.That(module.ChildNodes[0], Is.InstanceOf<AstClass>());

        var cls = (AstClass)module.ChildNodes[0];
        Assert.That(cls.BodyNode, Is.InstanceOf<AstBlock>());
        var clsBody = (AstBlock)cls.BodyNode;
        Assert.That(clsBody.ChildNodes.Count, Is.EqualTo(4));

        Assert.That(module.ChildNodes[1], Is.InstanceOf<AstFunctionDecl>());
        var fn = (AstFunctionDecl)module.ChildNodes[1];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(12));

        Assert.That(module.ChildNodes[2], Is.InstanceOf<AstFunctionDecl>());
    }
}
