#nullable enable
using System;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public static class TypeCaster
{
    public static bool CanImplicitCast(BifyType from, BifyType to)
    {
        if (from.Name == to.Name || from.Type == to.Type)
            return true;

        if (from.Name == "int" && to.Name == "float")
            return true;

        if (from.Name == "int" && to.Name == "double")
            return true;

        if (from.Name == "float" && to.Name == "double")
            return true;

        if (from.Name == "bool" && to.Name == "int")
            return true;

        if (from.Name == "int" && to.Name == "bool")
            return true;

        if (from.Name == "null" && to.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind)
            return true;

        if (from is ClassType fromClass && to is ClassType toClass)
            return fromClass.IsSubclassOf(toClass);

        if (from.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind && to.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind)
            return true;

        return false;
    }

    public static TypedValue ImplicitCast(CodeGenContext ctx, TypedValue value, BifyType targetType)
    {
        if (value.Type.Name == targetType.Name || value.Type.Type == targetType.Type)
            return value;

        if (value.Type.Name == "int" && targetType.Name == "float")
        {
            var casted = ctx.Builder.BuildSIToFP(value.Value, targetType.Type, "conv_itof");
            return new TypedValue(casted, targetType);
        }

        if (value.Type.Name == "int" && targetType.Name == "double")
        {
            var casted = ctx.Builder.BuildSIToFP(value.Value, targetType.Type, "conv_itod");
            return new TypedValue(casted, targetType);
        }

        if (value.Type.Name == "float" && targetType.Name == "double")
        {
            var casted = ctx.Builder.BuildFPExt(value.Value, targetType.Type, "conv_ftod");
            return new TypedValue(casted, targetType); 
        }

        if (value.Type.Name == "bool" && targetType.Name == "int")
        {
            var casted = ctx.Builder.BuildZExt(value.Value, targetType.Type, "conv_btoi");
            return new TypedValue(casted, targetType);
        }

        if (value.Type.Name == "int" && targetType.Name == "bool")
        {
            var zero = LLVMValueRef.CreateConstInt(value.Value.TypeOf, 0, false);
            var casted = ctx.Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, value.Value, zero, "conv_itob");
            return new TypedValue(casted, targetType);
        }

        if (value.Type is ClassType valClass && targetType is ClassType tgtClass)
        {
            if (valClass.IsSubclassOf(tgtClass))
                return new TypedValue(value.Value, targetType);

            new BifyTypeError($"Cannot implicitly convert type '{valClass.Name}' to '{tgtClass.Name}'").Throw();
        }

        if (value.Type.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind && targetType.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind)
        {
            if (value.Value.TypeOf == targetType.Type)
                return new TypedValue(value.Value, targetType);

            var casted = ctx.Builder.BuildPointerCast(value.Value, targetType.Type, "conv_ptr");
            return new TypedValue(casted, targetType);
        }

        new BifyTypeError($"Cannot implicitly convert type '{value.Type.Name}' to '{targetType.Name}'").Throw();
        return value;
    }

    public static TypedValue ExplicitCast(CodeGenContext ctx, TypedValue value, BifyType targetType)
    {
        if (value.Type.Name == targetType.Name || value.Type.Type == targetType.Type)
            return value;

        if (CanImplicitCast(value.Type, targetType))
            return ImplicitCast(ctx, value, targetType);

        if ((value.Type.Name == "float" || value.Type.Name == "double") && (targetType.Name == "int" || targetType.Name == "char"))
        {
            var casted = ctx.Builder.BuildFPToSI(value.Value, targetType.Type, "cast_fptosi");
            return new TypedValue(casted, targetType);
        }

        if (value.Type.Name == "double" && targetType.Name == "float")
        {
            var casted = ctx.Builder.BuildFPTrunc(value.Value, targetType.Type, "cast_fptrunc");
            return new TypedValue(casted, targetType);
        }

        if ((value.Type.Name == "float" || value.Type.Name == "double") && targetType.Name == "bool")
        {
            var zero = LLVMValueRef.CreateConstReal(value.Value.TypeOf, 0.0);
            var casted = ctx.Builder.BuildFCmp(LLVMRealPredicate.LLVMRealONE, value.Value, zero, "cast_fptobool");
            return new TypedValue(casted, targetType);
        }

        if (value.Type.Name == "bool" && (targetType.Name == "float" || targetType.Name == "double"))
        {
            var casted = ctx.Builder.BuildUIToFP(value.Value, targetType.Type, "cast_uitofp");
            return new TypedValue(casted, targetType);
        }

        if (value.Type.Name == "int" && targetType.Name == "char")
        {
            var casted = ctx.Builder.BuildTrunc(value.Value, targetType.Type, "cast_trunc");
            return new TypedValue(casted, targetType);
        }

        if (value.Type.Name == "char" && targetType.Name == "int")
        {
            var casted = ctx.Builder.BuildSExt(value.Value, targetType.Type, "cast_sext");
            return new TypedValue(casted, targetType);
        }

        if (value.Type.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind && targetType.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind)
        {
            if (value.Value.TypeOf == targetType.Type)
                return new TypedValue(value.Value, targetType);

            var casted = ctx.Builder.BuildPointerCast(value.Value, targetType.Type, "cast_ptr");
            return new TypedValue(casted, targetType);
        }

        if (value.Type.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind && targetType.Name == "int")
        {
            var casted = ctx.Builder.BuildPtrToInt(value.Value, targetType.Type, "cast_ptrtoi");
            return new TypedValue(casted, targetType);
        }

        if (value.Type.Name == "int" && targetType.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind)
        {
            var casted = ctx.Builder.BuildIntToPtr(value.Value, targetType.Type, "cast_itoptr");
            return new TypedValue(casted, targetType);
        }

        new BifyCastError($"Cannot cast '{value.Type.Name}' to '{targetType.Name}'").Throw();
        return value;
    }
}
