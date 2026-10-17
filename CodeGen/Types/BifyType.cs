using BifyLang.CodeGen.Core;
using LLVMSharp.Interop;

#nullable enable

namespace BifyLang.CodeGen.Types;

public class BifyType
{
    public string Name { get; private set; }

    public LLVMTypeRef Type { get; private set; }

    public BifyType(string name, LLVMTypeRef type)
    {
        Name = name;
        Type = type;
    }

    public static BifyType CreateInt(CodeGenContext ctx)
    {
        return new BifyType(
            "int",
            ctx.Context.Int32Type
        );
    }

    public static BifyType CreateFloat(CodeGenContext ctx)
    {
        return new BifyType(
            "float",
            ctx.Context.FloatType
        );
    }

    public static BifyType CreateBool(CodeGenContext ctx)
    {
        return new BifyType(
            "bool",
            ctx.Context.Int1Type
        );
    }

    public static BifyType CreateVoid(CodeGenContext ctx)
    {
        return new BifyType(
            "void",
            ctx.Context.VoidType
        );
    }

    public static BifyType CreateString(CodeGenContext ctx)
    {
        return new BifyType(
            "string",
            LLVMTypeRef.CreatePointer(ctx.Context.Int8Type, 0)
        );
    }

    public static BifyType CreateNull(CodeGenContext ctx)
    {
        return new BifyType(
            "null",
            LLVMTypeRef.CreatePointer(ctx.Context.Int8Type, 0)
        );
    }

    public static BifyType CreateDouble(CodeGenContext ctx)
    {
        return new BifyType(
            "double",
            ctx.Context.DoubleType);
    }

    public static BifyType CreateChar(CodeGenContext ctx)
    {
        return new BifyType("char", ctx.Context.Int8Type);
    }

    public static BifyType CreatePointer(LLVMTypeRef pointerType,CodeGenContext ctx)
    {
        return new BifyType("pointer", LLVMTypeRef.CreatePointer(pointerType, 0));
    }

    public override int GetHashCode() =>
        Name.GetHashCode();

    public override string ToString() =>
        Name;
}