
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

    public ClassInfo[] Resolve(AstModule module)
    {
        CollectClasses(module);
        VerifyInheritanceMap();

        foreach (var className in _classes.Keys)
        {
            Visit(className);
        }
        return _compilationOrder.ToArray();
    }



    private void CollectClasses(AstModule module)
    {
        foreach (AstNode node in module.ChildNodes)
        {
            if (node is not AstClass classNode)
                continue;

            string className = classNode.NameNode.Token.Value;

            string parentName = classNode.ParentNode?.Token.Value ?? "";

            var classInfo = new ClassInfo(className, classNode);

            if (_classes.ContainsKey(className))
            {
                new BifyNameError($"Class with name '{className}' already exists!").Throw();
                return;
            }
            _classes.Add(className, classInfo);
            _inheritanceMap.Add(className, parentName);
        }
    }

    private void VerifyInheritanceMap()
    {
        foreach (var (className, parentName) in _inheritanceMap)
        {
            if (parentName == null)
                continue;

            if (!_classes.ContainsKey(parentName) && parentName != string.Empty)
            {
                new BifyTypeError(
                    $"Class '{className}' inherits from unknown class '{parentName}'."
                ).Throw();
                return;
            }
        }
    }

    private void Visit(string className)
    {
        if (_states.TryGetValue(className, out var state))
        {
            if (state == VisitState.Visiting)
            {
                new BifyCircularInheritanceError(
                    $"Circular inheritance detected at class '{className}'."
                ).Throw();

                return;
            }

            if (state == VisitState.Visited)
                return;
        }

        _states[className] = VisitState.Visiting;

        string parentName = _inheritanceMap[className];

        if (parentName != string.Empty)
        {
            Visit(parentName);
        }

        _states[className] = VisitState.Visited;

        _compilationOrder.Add(_classes[className]);
    }
    public override string ToString()
    {
        return string.Join(
            Environment.NewLine,
            _compilationOrder.ConvertAll(c => c.Name)
        );
    }
}
