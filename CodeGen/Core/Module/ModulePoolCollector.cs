using System;
using System.Collections.Generic;
using System.IO;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Core.Module;

public class ModulePoolCollector(CodeGenContext ctx) : CodeGenBase(ctx)
{
    private readonly List<ModuleInfo> _modulePool = [];
    private readonly HashSet<string> _visitedPaths = new(StringComparer.OrdinalIgnoreCase);

    public ModuleInfo[] Collect(AstModule rootModule)
    {
        _modulePool.Clear();
        _visitedPaths.Clear();

        string rootFullPath = Path.GetFullPath(rootModule.ModulePath);
        _visitedPaths.Add(rootFullPath);

        using (_ctx.EnterModule(rootModule.Name, rootFullPath))
        {
            CollectRecursive(rootModule, rootFullPath);
        }

        BifyDebug.Log($"Modules collected: {_modulePool.Count}");
        foreach (var moduleInfo in _modulePool)
        {
            BifyDebug.Log($"Module: {moduleInfo.Name}");
        }
        return [.. _modulePool];
    }

    private void CollectRecursive(AstModule module, string currentFilePath)
    {
        var seenNonImport = false;
        foreach (var child in module.ChildNodes)
        {
            if (child is AstPoison)
                continue;

            _ctx.CurrentLine = child.LineNumber;
            BifyDebug.Log($"{seenNonImport}:{child.GetType().Name}");

            if (child is AstImport importNode)
            {
                if (seenNonImport)
                    Fail("Module import must always be on top.", DiagnosticKind.Import, importNode);

                string basePath = Path.GetDirectoryName(currentFilePath) ?? "";
                string fullPath = string.IsNullOrEmpty(basePath)
                    ? Path.GetFullPath(importNode.Path)
                    : Path.GetFullPath(Path.Combine(basePath, importNode.Path));

                if (!_visitedPaths.Add(fullPath))
                    continue;

                if (!File.Exists(fullPath))
                    Fail($"Module file not found: '{fullPath}'.", DiagnosticKind.Import, importNode);

                AstModule importedAst = CompilationHelper.ParseToAst(fullPath, _ctx.Diagnostics);

                _modulePool.Add(new ModuleInfo(importedAst.Name, fullPath, importedAst, new ModuleContext(_ctx, importedAst.Name, fullPath)));
                using (_ctx.EnterModule(importedAst.Name, fullPath))
                {
                    CollectRecursive(importedAst, fullPath);
                }
            }
            else
            {
                seenNonImport = true;
            }
        }
    }
}