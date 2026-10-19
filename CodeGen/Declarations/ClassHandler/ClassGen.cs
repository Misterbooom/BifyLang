#nullable enable
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Declarations.ClassHandler;

public class ClassGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public void EmitClass(AstClass astClass)
    {
        string name = astClass.NameNode.Token.Value;
        string? parentName = GetParentName(astClass.ParentNode);
        ClassType? parentType = null;
        if (parentName != null)
        {
            var bifyType = _ctx.TypeRegistry.GetBifyType(parentName);
            if (bifyType is ClassType pt)
                parentType = pt;
            else
                new BifyTypeError($"Base type '{parentName}' is not a class").Throw();
        }

        var ptrType = LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0);
        var strongCount = BifyType.CreateInt(_ctx).Type;
        var weakCount = BifyType.CreateInt(_ctx).Type;
        List<LLVMTypeRef> elementTypes = [ptrType, strongCount, weakCount];

        var classType = new ClassType(name, ptrType, default, parentType);

        if (parentType != null)
        {
            foreach (var attr in parentType.Fields.Values.Where(f => !f.IsMethod).OrderBy(f => f.Index))
            {
                classType.Fields.Add(attr.Name, attr);
                elementTypes.Add(attr.Type.Type);
            }

            foreach (var mName in parentType.VTableMethodNames)
            {
                classType.VTableMethodNames.Add(mName);
                classType.Fields.Add(mName, parentType.Fields[mName]);
            }

            if (parentType.Fields.TryGetValue("constructor", out var parentCtor))
            {
                classType.Fields["constructor"] = parentCtor;
            }
        }

        int startAttrIndex = elementTypes.Count;
        var ownAttributes = ClassFieldParser.ParseAttributes(astClass, _ctx, startAttrIndex, name);
        foreach (var attr in ownAttributes)
        {
            if (classType.Fields.ContainsKey(attr.Key))
                classType.Fields[attr.Key] = attr.Value;
            else
                classType.Fields.Add(attr.Key, attr.Value);
            elementTypes.Add(attr.Value.Type.Type);
        }

        LLVMTypeRef named = _ctx.Context.CreateNamedStruct(name);
        named.StructSetBody(elementTypes.ToArray(), false);

        classType.StructType = named;
        _ctx.TypeRegistry.Register(name, classType);

        ClassFieldParser.ParseMethods(astClass, classType, _ctx);

        BuildVTable(classType, name);
    }

    private static string? GetParentName(AstNode? node)
    {
        if (node is AstLine line)
            node = line.Child;
        if (node is AstIdentifier id)
            return id.Name;
        return null;
    }

    private void BuildVTable(ClassType classType, string name)
    {
        var ptrType = LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0);
        int count = classType.VTableMethodNames.Count;
        var vtableArrayType = LLVMTypeRef.CreateArray(ptrType, (uint)count);
        var vtableGlobal = _ctx.Module.AddGlobal(vtableArrayType, $"{name}.vtable");
        vtableGlobal.IsGlobalConstant = true;
        vtableGlobal.Linkage = LLVMLinkage.LLVMInternalLinkage;

        if (count > 0)
        {
            var fnPtrs = new LLVMValueRef[count];
            for (int i = 0; i < count; i++)
            {
                string mName = classType.VTableMethodNames[i];
                var mField = classType.Fields[mName];
                fnPtrs[i] = mField.Value;
            }
            vtableGlobal.Initializer = LLVMValueRef.CreateConstArray(ptrType, fnPtrs);
        }
        else
        {
            vtableGlobal.Initializer = LLVMValueRef.CreateConstNull(vtableArrayType);
        }

        classType.VTableGlobal = vtableGlobal;
        classType.VTableType = vtableArrayType;
    }
}