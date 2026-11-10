#nullable enable
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public class ClassLayoutBuilder(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public void BuildClassesLayout(ClassInfo[] classes)
    {
        foreach (var info in classes)
        {
            DeclareOpaque(info);
        }

        foreach (var info in classes)
        {
            BuildBody(info);
        }
    }

    private void DeclareOpaque(ClassInfo info)
    {
        var structType = _ctx.Context.CreateNamedStruct(info.Name);
        var pointerType = LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0);

        ClassType? parent = null;
        string parentName = info.classNode.ParentNode?.Token.Value ?? "";
        if (!string.IsNullOrEmpty(parentName))
        {
            if (_ctx.TypeRegistry.TryGetType(parentName, out var pType) && pType is ClassType cp)
                parent = cp;
            else
                new BifyNameError($"Class '{info.Name}' inherits from unknown class '{parentName}'.").Throw();
        }

        var classType = new ClassType(info.Name, pointerType, structType, parent);
        _ctx.TypeRegistry.Register(info.Name, classType);
    }

    private void BuildBody(ClassInfo info)
    {
        var classType = (ClassType)_ctx.TypeRegistry.GetBifyType(info.Name);

        InheritMembers(classType);
        ProcessFields(info, classType);
        ProcessMethods(info, classType);
        FinalizeStructBody(classType);
    }

    private static void InheritMembers(ClassType classType)
    {
        var parent = classType.Parent;
        classType.VTable = VTableBuilder.Inherit(parent);

        if (parent == null)
            return;

        foreach (var kvp in parent.Fields)
            classType.Fields.Add(kvp.Key, kvp.Value);

        foreach (var kvp in parent.Methods)
        {
            if (kvp.Key != "constructor")
                classType.Methods.Add(kvp.Key, kvp.Value);
        }
    }

    private void ProcessFields(ClassInfo info, ClassType classType)
    {
        var block = (AstBlock)info.classNode.BodyNode;
        int nextIndex = 3 + classType.Fields.Count;

        foreach (var varDecl in block.ChildNodes.OfType<AstVarDecl>())
        {
            string name = varDecl.AssignmentNode.Left.Token.Value;

            if (classType.Fields.ContainsKey(name))
                new BifyNameError($"Duplicate field '{name}' in class '{info.Name}'.").Throw();

            if (classType.Methods.ContainsKey(name))
                new BifyNameError($"Field '{name}' in class '{info.Name}' conflicts with existing method.").Throw();

            var field = ClassMemberCollector.CollectField(varDecl, _ctx, info.Name, nextIndex++);
            classType.Fields.Add(name, field);
        }
    }

    private void ProcessMethods(ClassInfo info, ClassType classType)
    {
        var block = (AstBlock)info.classNode.BodyNode;

        foreach (var funcDecl in block.ChildNodes.OfType<AstFunctionDecl>())
        {
            string name = funcDecl.FunctionNameNode.Name;

            if (classType.Fields.ContainsKey(name))
                new BifyNameError($"Method '{name}' in class '{info.Name}' conflicts with existing field.").Throw();

            var method = ClassMemberCollector.CollectMethod(funcDecl, _ctx, info.Name);
            int vtableIndex = VTableBuilder.Apply(classType.VTable, method, classType.Parent);
            method.VTableIndex = vtableIndex;

            if (classType.Methods.ContainsKey(name) && !method.IsOverride)
                new BifyNameError($"Duplicate method '{name}' in class '{info.Name}'.").Throw();

            classType.Methods[name] = method;
        }
    }

    private void FinalizeStructBody(ClassType classType)
    {
        var elementTypes = new List<LLVMTypeRef>
        {
            LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0),
            _ctx.Context.Int32Type,
            _ctx.Context.Int32Type
        };

        foreach (var field in classType.Fields.Values.OrderBy(f => f.Index))
        {
            elementTypes.Add(field.Type.Type);
        }

        classType.StructType.StructSetBody(elementTypes.ToArray(), false);
    }
}