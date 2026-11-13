#nullable enable
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public static class MemberResolver
{

    public static LLVMValueRef ResolveFieldPtr(CodeGenContext ctx, ClassType classType, LLVMValueRef instance, ClassField field) =>
        ctx.Builder.BuildStructGEP2(classType.StructType, instance, (uint)field.Index, field.Name);

  
    public static bool TryResolveThisField(CodeGenContext ctx, string name, out LLVMValueRef fieldPtr, out ClassField field)
    {
        fieldPtr = default;
        field = default;
        if (!ctx.CurrentScope.TryLookup("this", out var thisSym) ||
            thisSym.Type is not ClassType ct ||
            !ct.Fields.TryGetValue(name, out var found))
            return false;

        field = found;
        CheckAccess(ctx, ct,field.AccessLevel, field.Name);
        var thisVal = ctx.Builder.BuildLoad2(thisSym.Type.Type, thisSym.Pointer, "this");
        fieldPtr = ResolveFieldPtr(ctx, ct, thisVal, field);
        return true;
    }

    public static void CheckAccess(CodeGenContext ctx, ClassType classType, AccessLevel accessLevel,  string memberName)
    {

        if (accessLevel == AccessLevel.PRIVATE)
        {
            if (!ctx.CurrentScope.TryLookup("this", out var thisSym) ||
                thisSym.Type is not ClassType ct ||
                ct.FullName != classType.FullName)
            {
                new BifyAttributeError($"Cannot access private member '{memberName}' of class '{classType.FullName}'").Throw();
            }
        }
        else if (accessLevel == AccessLevel.PROTECTED)
        {
            bool allowed = false;
            if (ctx.CurrentScope.TryLookup("this", out var thisSym) && thisSym.Type is ClassType ct)
            {
                if (ctx.TypeRegistry.TryGetType(classType.FullName, out var declType) && declType is ClassType declClassType)
                {
                    allowed = ct.IsSubclassOf(declClassType);
                }
            }

            if (!allowed)
            {
                new BifyAttributeError($"Cannot access protected member '{memberName}' of class '{classType.FullName}'").Throw();
            }
        }
    }
}