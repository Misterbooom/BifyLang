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
    public Dictionary<string, ClassType> ClassesInfo = new Dictionary<string, ClassType>();

    public ModuleContext(CodeGenContext ctx, string name, string fullpath)
    {
        
        _ctx = ctx;
        Name = name;
        FullPath = fullpath;
        
    }
 



}