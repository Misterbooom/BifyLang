using BifyLang.CodeGen.Core.Module;

namespace BifyLang.CodeGen.Core.Types.Data;

public class ModuleType(string name, ModuleContext moduleContext):BifyType(name, null)
{
    public readonly ModuleContext ModuleContext = moduleContext; 
}