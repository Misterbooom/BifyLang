#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Module;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public class ClassLayoutBuilder(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public void BuildClassesLayout(ClassInfo[] classes)
    {
        List<ClassType> classTypes = [];
        
        foreach (var info in classes)
        {
            var ownerCtx = _ctx.ModulesMap[info.ModuleName].ModuleContext;
            using (_ctx.EnterModule(ownerCtx))
                classTypes.Add(DeclareOpaque(info));
                
        }

        for(int i =0; i < classes.Length; i++)
        {
            var ownerCtx = _ctx.ModulesMap[classes[i].ModuleName].ModuleContext;
            using (_ctx.EnterModule(ownerCtx))
               BuildBody(classTypes[i], classes[i]);
        }
    }

    private ClassType DeclareOpaque(ClassInfo info)
    {
        var structType = _ctx.Context.CreateNamedStruct(info.Name);
        var pointerType = LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0);

        ClassType? parent = null;
        string parentName = ClassDependencyResolver.ResolveParentName(info.ClassNode.ParentNode, info.ModuleName);
        if (!string.IsNullOrEmpty(parentName))
        {
            if (_ctx.TypeRegistry.TryGetType(parentName, out var pType) && pType is ClassType cp)
                parent = cp;
            else
                new BifyTypeError($"Class '{info.Name}' inherits from unknown class '{parentName}'.").Throw();
        }

        string mangledName = NameMangler.GenerateClassName(info.ModuleName, info.Name);
        var classType = new ClassType(info.Name, mangledName, pointerType, structType,info.ModuleName, parent);
        _ctx.TypeRegistry.Register(mangledName, classType);
        _ctx.ModuleContext.ClassesInfo[mangledName] = classType;
        return classType;
    }

    private void BuildBody(ClassType type, ClassInfo info)
    {
        BifyDebug.Log($"{_ctx.TypeRegistry}");
        
        var classType = _ctx.TypeRegistry.GetBifyType(type.FullName) as ClassType;
        if (classType == null)
        {
            throw new Exception($"Class: '{type.FullName}' was not registred in TypeRegistry.");
        }

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
            if (kvp.Key != "constructor" && kvp.Key != DestructorSynthesizer.DestructorName)
                classType.Methods.Add(kvp.Key, kvp.Value);
        }
    }

    private void ProcessFields(ClassInfo info, ClassType classType)
    {
        var block = (AstBlock)info.ClassNode.BodyNode;
        int nextIndex = 3 + classType.Fields.Count;

        foreach (var varDecl in block.ChildNodes.OfType<AstVarDecl>())
        {
            string name = varDecl.AssignmentNode.Left.Token.Value;

            if (classType.Fields.ContainsKey(name))
                new BifyNameError($"Duplicate field '{name}' in class '{info.Name}'.").Throw();

            if (classType.Methods.ContainsKey(name) || name == DestructorSynthesizer.DestructorName)
                new BifyNameError($"Field '{name}' in class '{info.Name}' conflicts with existing method.").Throw();

            var field = ClassMemberCollector.CollectField(varDecl, _ctx, classType, nextIndex++);
            classType.Fields.Add(name, field);
        }
    }

    private void ProcessMethods(ClassInfo info, ClassType classType)
    {
        var block = (AstBlock)info.ClassNode.BodyNode;
        var funcDecls = block.ChildNodes.OfType<AstFunctionDecl>().ToList();

        ProcessDestructor(info, classType, funcDecls);

        foreach (var funcDecl in funcDecls)
        {
            string name = funcDecl.FunctionNameNode.Name;
            if (name == DestructorSynthesizer.DestructorName)
                continue;

            if (classType.Fields.ContainsKey(name))
                new BifyNameError($"Method '{name}' in class '{classType.FullName}' conflicts with existing field.").Throw();

            var method = ClassMemberCollector.CollectMethod(funcDecl, _ctx, classType);
            int vtableIndex = VTableBuilder.Apply(classType.VTable, method, classType.Parent);
            method.VTableIndex = vtableIndex;

            if (classType.Methods.ContainsKey(name) && !method.IsOverride)
                new BifyNameError($"Duplicate method '{name}' in class '{classType.FullName}'.").Throw();

            classType.Methods[name] = method;
        }
    }

    private void ProcessDestructor(ClassInfo info, ClassType classType, List<AstFunctionDecl> funcDecls)
    {
        var dtors = funcDecls.Where(f => f.FunctionNameNode.Name == DestructorSynthesizer.DestructorName).ToList();
        if (dtors.Count > 1)
        {
            new BifyNameError($"Class '{info.Name}' cannot have multiple destructors.").Throw();
        }

        var method = dtors.Count == 1
            ? ClassMemberCollector.CollectMethod(dtors[0], _ctx, classType)
            : DestructorSynthesizer.CreateDefault(_ctx, classType);

        method.VTableIndex = VTableBuilder.ApplyDestructor(classType.VTable, method);
        classType.Methods[DestructorSynthesizer.DestructorName] = method;
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