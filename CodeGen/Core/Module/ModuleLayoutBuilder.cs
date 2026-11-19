using BifyLang.Ast;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Declarations;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Core.Module;

public class ModuleLayoutBuilder:LayoutBuilder
{
    private readonly LayoutBuilder[] _builders;
    public ModuleLayoutBuilder(CodeGenContext ctx) : base(ctx)
    {
        _builders = [
            new StructLayoutBuilder(_ctx),
            new ClassLayoutBuilder(_ctx),
            new FunctionLayoutBuilder(_ctx),
        ];
    }

    public override void Declare(ModuleInfo[] modules)
    {
        foreach (var m in modules)
        {
            var modType = new ModuleType(m.Name, m.ModuleContext);
            _ctx.ModulesMap[m.Name] = modType;
            _ctx.TypeRegistry.Register(m.Name, modType);
        }
        foreach (var builder in _builders)
        {
            builder.Declare(modules);
        }
    }

    public override void Build(ModuleInfo[] modules)
    {
        Declare(modules);
        foreach (var builder in _builders)
        {
            builder.Build(modules);
        }
        BuildModuleLayout(modules);
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
        using (_ctx.EnterModule(module.ModuleContext))
        {
            var declarationDispatcher = new DeclarationDispatcher(_ctx);
            declarationDispatcher.EmitModule(module.ModuleNode);
        }
    }
}
