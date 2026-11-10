using BifyLang.CodeGen.Types;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public static class BuiltinFunctions
{
    public static void DeclarePrintf(CodeGenContext ctx)
    {
        var fnType = LLVMTypeRef.CreateFunction(
            BifyType.CreateInt(ctx).Type, 
            [BifyType.CreatePointer(BifyType.CreateChar(ctx).Type, ctx).Type], 
            IsVarArg: true 
        );
        var val = ctx.Module.AddFunction("printf", fnType);
        ctx.CurrentScope.Define("printf", val, new BifyType("function", fnType));
    }
    
    // public static void DeclareMalloc(CodeGenContext ctx)
    // {
    //     var fnType = LLVMTypeRef.CreateFunction(
    //         BifyType.CreatePointer(BifyType.CreateVoid(ctx).Type, ctx).Type, 
    //         [ctx.Context.Int64Type], 
    //         IsVarArg: false
    //     );
    //     var fn = ctx.Module.AddFunction("malloc", fnType);
    //     ctx.CurrentScope.Define("malloc", fn, new BifyType("function", fnType));
    // }
}