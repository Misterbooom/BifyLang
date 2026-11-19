using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Module;

namespace BifyLang.CodeGen.Core;

public abstract class LayoutBuilder(CodeGenContext ctx):CodeGenBase(ctx)
{
    public abstract void Build(ModuleInfo[] modules);
    public abstract void Declare(ModuleInfo[] modules);

}
