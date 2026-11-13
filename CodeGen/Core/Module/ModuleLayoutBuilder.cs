using BifyLang.Ast;
using BifyLang.CodeGen.Declarations;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Core.Module;

public class ModuleLayoutBuilder(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public void Build(ModuleInfo[] modules, ClassInfo[] classes)
    {
        new ClassLayoutBuilder(_ctx).BuildClassesLayout(classes);

        foreach (var moduleInfo in modules)
        {
            using (_ctx.EnterModule(moduleInfo.ModuleContext))
            {
                BifyDebug.Log("Module: " + moduleInfo.Name);
                _ctx.ModulesMap[moduleInfo.Name] = new ModuleType(moduleInfo.Name, moduleInfo.ModuleContext);
                DeclareFunctions(moduleInfo.ModuleNode, moduleInfo.ModuleContext);
                _ctx.TypeRegistry.Register(moduleInfo.Name, new ModuleType(moduleInfo.Name, moduleInfo.ModuleContext));
            }
        }

        BuildModuleLayout(modules);
    }

    private void DeclareFunctions(AstModule module, ModuleContext moduleContext)
    {
        var funcGen = new FunctionGen(_ctx);

        foreach (var moduleChildNode in module.ChildNodes)
        {
            if (moduleChildNode is AstFunctionDecl functionChildNode)
            {
                BifyDebug.Log("Declaring function: " + functionChildNode.FunctionNameNode.Name);
                funcGen.DeclareFunction(functionChildNode);
            }
        }
    }

    private void BuildModuleLayout(ModuleInfo[] modules)
    {
        foreach (var moduleInfo in modules)
        {
            GenerateModule(moduleInfo);
        }
    }

    private void GenerateModule(ModuleInfo module)
    {
        string[] savedCode = Traceback.Instance.Source;
        string savedFilePath = Traceback.Instance.FilePath;

        try
        {
            if (module.ModuleNode.SourceCode is { Length: > 0 })
            {
                Traceback.Instance.Source = module.ModuleNode.SourceCode;
            }

            Traceback.Instance.FilePath = module.FullPath;

            using (_ctx.EnterModule(module.ModuleContext))
            {
                var declarationDispatcher = new DeclarationDispatcher(_ctx);
                declarationDispatcher.EmitModule(module.ModuleNode);
            }
        }
        finally
        {
            Traceback.Instance.FilePath = savedFilePath;
            Traceback.Instance.Source = savedCode;
        }
    }
}