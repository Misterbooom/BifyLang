using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Core;

public class ClassDependencyResolver
{
    private enum VisitState
    {
        Visiting,
        Visited
    }

    private readonly Dictionary<string, ClassInfo> _classes = [];
    private readonly Dictionary<string, string> _inheritanceMap = [];
    private readonly Dictionary<string, VisitState> _states = [];
    private readonly List<ClassInfo> _compilationOrder = [];

    public ClassInfo[] Resolve(IEnumerable<AstModule> modules)
    {
        _classes.Clear();
        _inheritanceMap.Clear();
        _states.Clear();
        _compilationOrder.Clear();

        foreach (var astModule in modules)
        {
            CollectClasses(astModule);
        }

        VerifyInheritanceMap();

        foreach (var mangledName in _classes.Keys)
        {
            Visit(mangledName);
        }

        return _compilationOrder.ToArray();
    }

    private void CollectClasses(AstModule module)
    {
        foreach (AstNode node in module.ChildNodes)
        {
            if (node is not AstClass classNode)
                continue;

            string rawClassName = classNode.NameNode.Token.Value;
            string mangledName = NameMangler.GenerateClassName(module.Name, rawClassName);

            if (_classes.ContainsKey(mangledName))
            {
                new BifyNameError($"Class '{rawClassName}' already exists in module '{module.Name}'.").Throw();
                return;
            }

            string mangledParentName = ResolveParentName(classNode.ParentNode, module.Name);
            var classInfo = new ClassInfo(rawClassName, classNode, module.Name);

            _classes.Add(mangledName, classInfo);
            _inheritanceMap.Add(mangledName, mangledParentName);
        }
    }

    public static string ResolveParentName(AstNode? node, string currentModuleName)
    {
        return node switch
        {
            null => string.Empty,
            AstIdentifier id => NameMangler.GenerateClassName(currentModuleName, id.Name),
            AstMemberAccess { Left: AstIdentifier moduleId, Right: AstIdentifier classId } 
                => NameMangler.GenerateClassName(moduleId.Name, classId.Name),
            AstMemberAccess 
                => new BifySyntaxError("Invalid parent class reference.").Throw<string>(),
            _ => throw new NotSupportedException($"'{node.GetType().Name}' is not supported as a parent class node.")
        };
    }

    private void VerifyInheritanceMap()
    {
        foreach (var (mangledClassName, mangledParentName) in _inheritanceMap)
        {
            if (string.IsNullOrEmpty(mangledParentName))
                continue;

            if (!_classes.ContainsKey(mangledParentName))
            {
                new BifyTypeError(
                    $"Class '{mangledClassName}' inherits from unknown class '{mangledParentName}'."
                ).Throw();
                return;
            }
        }
    }

    private void Visit(string mangledClassName)
    {
        if (_states.TryGetValue(mangledClassName, out var state))
        {
            if (state == VisitState.Visiting)
            {
                new BifyCircularInheritanceError(
                    $"Circular inheritance detected at class '{mangledClassName}'."
                ).Throw();
                return;
            }

            if (state == VisitState.Visited)
                return;
        }

        _states[mangledClassName] = VisitState.Visiting;

        string mangledParentName = _inheritanceMap[mangledClassName];
        if (!string.IsNullOrEmpty(mangledParentName))
        {
            Visit(mangledParentName);
        }

        _states[mangledClassName] = VisitState.Visited;
        _compilationOrder.Add(_classes[mangledClassName]);
    }

    public override string ToString()
    {
        return string.Join(
            Environment.NewLine,
            _compilationOrder.ConvertAll(c => $"{c.ModuleName}.{c.Name}:{_inheritanceMap[NameMangler.GenerateClassName(c.ModuleName,c.Name)]}")
        );
    }
}