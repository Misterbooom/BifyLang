using System.Collections.Generic;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;

namespace BifyLang.CodeGen.Core.Module;

public class ModuleContext
{
    private  CodeGenContext _ctx;
    public readonly string Name;
    public readonly string FullPath;
    public Scope CurrentScope = new Scope(parent: null);
    public readonly Dictionary<string, ICompositeType> TypesInfo = new Dictionary<string, ICompositeType>();

    public ModuleContext(CodeGenContext ctx, string name, string fullpath)
    {

        _ctx = ctx;
        Name = name;
        FullPath = fullpath;

    }




}
