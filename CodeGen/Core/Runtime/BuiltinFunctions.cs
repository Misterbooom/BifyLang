using System;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Runtime;

public static class BuiltinFunctions
{
    public static void DeclarePrintf(CodeGenContext ctx)
    {
        var intType = BifyType.CreateInt(ctx);
        var charPtrType = BifyType.CreatePointer(BifyType.CreateChar(ctx).Type, ctx);

        var fnType = LLVMTypeRef.CreateFunction(
            intType.Type,
            [charPtrType.Type],
            IsVarArg: true);

        LLVMValueRef func = ctx.Module.GetNamedFunction("printf");
        if (func.Handle == IntPtr.Zero)
        {
            func = ctx.Module.AddFunction("printf", fnType);
        }

        if (!ctx.CurrentScope.TryLookup("printf", out _))
        {
            var callableType = new CallableType(
                name: "printf",
                fullName: "printf",
                parametersType: [new ParameterInfo("format", charPtrType, false)],
                returnType: intType,
                functionType: fnType);

            ctx.CurrentScope.Define("printf", func, callableType);
        }
    }

    public static (LLVMValueRef Func, LLVMTypeRef Type) GetOrDeclareStrcmp(CodeGenContext ctx)
    {
        var charPtrType = BifyType.CreatePointer(BifyType.CreateChar(ctx).Type, ctx).Type;
        var fnType = LLVMTypeRef.CreateFunction(BifyType.CreateInt(ctx).Type, [charPtrType, charPtrType], IsVarArg: false);
        var func = ctx.Module.GetNamedFunction("strcmp");
        if (func.Handle == IntPtr.Zero)
            func = ctx.Module.AddFunction("strcmp", fnType);

        return (func, fnType);
    }
}