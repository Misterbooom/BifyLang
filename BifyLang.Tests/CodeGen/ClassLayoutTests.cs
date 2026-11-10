#nullable enable
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;
using LLVMSharp.Interop;
using NUnit.Framework;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class ClassLayoutTests : BifyTestBase
{
    [Test]
    public void ClassLayout_StructAndFieldIndices()
    {
        string code = """
        class Base {
            public int x;
            public float y;
        }
        class Derived : Base {
            public bool z;
        }
        void main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();

        var baseType = (ClassType)m.Context.TypeRegistry.GetBifyType("Base");
        var derivedType = (ClassType)m.Context.TypeRegistry.GetBifyType("Derived");

        Assert.That(baseType.Fields["x"].Index, Is.EqualTo(3));
        Assert.That(baseType.Fields["y"].Index, Is.EqualTo(4));

        Assert.That(derivedType.Fields["x"].Index, Is.EqualTo(3));
        Assert.That(derivedType.Fields["y"].Index, Is.EqualTo(4));
        Assert.That(derivedType.Fields["z"].Index, Is.EqualTo(5));

        Assert.That(baseType.StructType.StructElementTypesCount, Is.EqualTo(5));
        Assert.That(derivedType.StructType.StructElementTypesCount, Is.EqualTo(6));

        Assert.That(baseType.Fields["x"].Type.Name, Is.EqualTo("int"));
        Assert.That(baseType.Fields["y"].Type.Name, Is.EqualTo("float"));
        Assert.That(derivedType.Fields["z"].Type.Name, Is.EqualTo("bool"));
    }

    [Test]
    public void ClassLayout_VirtualAndOverrideVTable()
    {
        string code = """
        class Animal {
            public virtual int speak() { return 1; }
            public int walk() { return 0; }
        }
        class Dog : Animal {
            public override int speak() { return 2; }
            public virtual void bark() {}
        }
        void main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();

        var animal = (ClassType)m.Context.TypeRegistry.GetBifyType("Animal");
        var dog = (ClassType)m.Context.TypeRegistry.GetBifyType("Dog");

        Assert.That(animal.VTable.Slots.Count, Is.EqualTo(1));
        Assert.That(animal.VTable.Slots[0].MethodName, Is.EqualTo("speak"));
        Assert.That(animal.Methods["speak"].VTableIndex, Is.EqualTo(0));
        Assert.That(animal.Methods["walk"].VTableIndex, Is.EqualTo(-1));

        Assert.That(dog.VTable.Slots.Count, Is.EqualTo(2));
        Assert.That(dog.VTable.Slots[0].MethodName, Is.EqualTo("speak"));
        Assert.That(dog.VTable.Slots[0].Method.DeclaringClass, Is.EqualTo("Dog"));
        Assert.That(dog.Methods["speak"].VTableIndex, Is.EqualTo(0));

        Assert.That(dog.VTable.Slots[1].MethodName, Is.EqualTo("bark"));
        Assert.That(dog.Methods["bark"].VTableIndex, Is.EqualTo(1));
    }

    [Test]
    public void ClassLayout_MutualClassReferences()
    {
        string code = """
        class NodeA {
            public NodeB next;
        }
        class NodeB {
            public NodeA prev;
        }
        void main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();

        var nodeA = (ClassType)m.Context.TypeRegistry.GetBifyType("NodeA");
        var nodeB = (ClassType)m.Context.TypeRegistry.GetBifyType("NodeB");

        Assert.That(nodeA.Fields["next"].Type, Is.SameAs(nodeB));
        Assert.That(nodeB.Fields["prev"].Type, Is.SameAs(nodeA));
    }

    [Test]
    public void ClassLayout_OverrideWithoutBaseMethod_Throws()
    {
        string code = """
        class Foo {
            public override void bar() {}
        }
        void main() {}
        """;

        AssertCompileError<BifyTypeError>(code);
    }

    [Test]
    public void ClassLayout_VirtualMethodAlreadyExists_Throws()
    {
        string code = """
        class A {
            public virtual void bar() {}
        }
        class B : A {
            public virtual void bar() {}
        }
        void main() {}
        """;

        AssertCompileError<BifyTypeError>(code);
    }

    [Test]
    public void ClassLayout_HidingVirtualWithoutOverride_Throws()
    {
        string code = """
        class A {
            public virtual void bar() {}
        }
        class B : A {
            public void bar() {}
        }
        void main() {}
        """;

        AssertCompileError<BifyTypeError>(code);
    }

    [Test]
    public void ClassLayout_OverrideSignatureMismatch_Throws()
    {
        string code = """
        class A {
            public virtual int bar(int x) { return x; }
        }
        class B : A {
            public override float bar(int x) { return 0.0; }
        }
        void main() {}
        """;

        AssertCompileError<BifyTypeError>(code);
    }

    [Test]
    public void ClassLayout_DuplicateField_Throws()
    {
        string code = """
        class A {
            public int x;
        }
        class B : A {
            public int x;
        }
        void main() {}
        """;

        AssertCompileError<BifyNameError>(code);
    }

    [Test]
    public void ClassLayout_ConstructorCannotBeVirtual_Throws()
    {
        string code = """
        class A {
            public virtual constructor() {}
        }
        void main() {}
        """;

        AssertCompileError<BifyTypeError>(code);
    }
}
