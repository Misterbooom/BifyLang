using System;
using System.Collections.Generic;
using System.IO;
using BifyLang.Ast;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Core.Module;

public class ModulePoolCollector(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;
    private readonly List<ModuleInfo> _modulePool = [];
    private readonly HashSet<string> _visitedPaths = new(StringComparer.OrdinalIgnoreCase);

    public ModuleInfo[] Collect(AstModule rootModule)
    {
        _modulePool.Clear();
        _visitedPaths.Clear();

        string rootFullPath = Path.GetFullPath(rootModule.ModulePath);
        _visitedPaths.Add(rootFullPath);

        // _modulePool.Add(new  ModuleInfo(rootModule.Name, rootFullPath, rootModule, new ModuleContext(ctx, rootModule.ModuleName, rootFullPath)));
        CollectRecursive(rootModule, rootFullPath);
        BifyDebug.Log($"Modules collected: {_modulePool.Count}");
        foreach (var moduleInfo in _modulePool)
        {
            BifyDebug.Log($"Module: {moduleInfo.Name}");
        }
        Traceback.Instance.FilePath = rootModule.ModulePath;
        Traceback.Instance.Source = rootModule.SourceCode;
        return [.. _modulePool];
    }

    private void CollectRecursive(AstModule module, string currentFilePath)
    {
        var seenNonImport = false;
        foreach (var child in module.ChildNodes)
        {
            Traceback.Instance.SetCurrentLine(child.LineNumber);
            BifyDebug.Log($"{seenNonImport}:{child.GetType().Name}");

            if (child is AstImport importNode)
            {
                if (seenNonImport)
                {
                    new BifyImportError("Module import must always be on top.")
                        .Throw<ModuleInfo[]>();
                    return;
                }

                string basePath = Path.GetDirectoryName(currentFilePath) ?? "";
                string fullPath = string.IsNullOrEmpty(basePath)
                    ? Path.GetFullPath(importNode.Path)
                    : Path.GetFullPath(Path.Combine(basePath, importNode.Path));

                if (!_visitedPaths.Add(fullPath))
                {
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    new BifyImportError($"Module file not found: '{fullPath}'.")
                        .Throw<ModuleInfo[]>();
                    return;
                }

                int savedLine = Traceback.Instance.Line;
                string savedFile = Traceback.Instance.FilePath;
                string[] savedSource = Traceback.Instance.Source;

                AstModule importedAst = CompilationHelper.ParseToAst(fullPath);

                _modulePool.Add(new ModuleInfo(importedAst.Name, fullPath, importedAst, new ModuleContext(_ctx, importedAst.Name, fullPath)));
                CollectRecursive(importedAst, fullPath);

                Traceback.Instance.FilePath = savedFile;
                Traceback.Instance.SetCurrentLine(savedLine);
                Traceback.Instance.Source = savedSource;
            }
            else
            {
                seenNonImport = true;
            }
        }
    }
}