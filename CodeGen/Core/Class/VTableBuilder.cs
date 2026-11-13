#nullable enable
using System;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public static class VTableBuilder
{
    public static VTable Inherit(ClassType? parent)
    {
        return parent?.VTable != null ? parent.VTable.Clone() : new VTable();
    }

    public static int ApplyDestructor(VTable vtable, ClassMethod destructor)
    {
        if (vtable.Slots.Count == 0)
        {
            return vtable.AddMethod(destructor);
        }

        vtable.Replace(0, destructor);
        return 0;
    }

    public static int Apply(VTable vtable, ClassMethod method, ClassType? parent)
    {
        if (method.Name == DestructorSynthesizer.DestructorName)
        {
            return ApplyDestructor(vtable, method);
        }

        bool hasParentSlot = vtable.TryFind(method.Name, out var slot);

        if (method.IsOverride)
        {
            if (!hasParentSlot || slot == null)
            {
                new BifyTypeError(
                    $"Method '{method.Name}' is marked 'override' but no virtual method with this name was found in base classes."
                ).Throw();
                return -1;
            }

            VerifyOverrideSignature(slot.Method, method);
            vtable.Replace(slot.Index, method);
            return slot.Index;
        }

        if (method.IsVirtual)
        {
            if (hasParentSlot)
            {
                new BifyTypeError(
                    $"Method '{method.Name}' is marked 'virtual' but already exists in base class VTable. Use 'override' instead."
                ).Throw();
                return -1;
            }

            return vtable.AddMethod(method);
        }

        if (hasParentSlot)
        {
            new BifyTypeError(
                $"Method '{method.Name}' hides inherited virtual method without 'override' flag."
            ).Throw();
            return -1;
        }

        return -1;
    }

    private static void VerifyOverrideSignature(ClassMethod parentMethod, ClassMethod overrideMethod)
    {
        if (parentMethod.Signature.Type != overrideMethod.Signature.Type)
        {
            new BifyTypeError(
                $"Override method '{overrideMethod.Name}' signature does not match base method '{parentMethod.DeclaringClass}.{parentMethod.Name}'."
            ).Throw();
        }
    }
    public static void Build(VTable vTable,CodeGenContext _ctx, string className)
    {
        var ptrType = LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0);
        int count = vTable.Slots.Count;
        var vtableArrayType = LLVMTypeRef.CreateArray(ptrType, (uint)count);
        var vtableGlobal = _ctx.Module.AddGlobal(vtableArrayType, NameMangler.GenerateVtable(_ctx.ModuleContext.Name, className));
        vtableGlobal.IsGlobalConstant = true;
        vtableGlobal.Linkage = LLVMLinkage.LLVMInternalLinkage;
        if (count > 0)
        {
            var fnPtrs = new LLVMValueRef[count];
            for (int i = 0; i < count; i++)
            {
                var slot = vTable.Slots[i];
                var fn = slot.Method.FunctionValue;
                if (fn.Handle == IntPtr.Zero)
                {
                    string symbol = NameMangler.GenerateMethodName(_ctx.ModuleContext.Name, className, slot.MethodName);
                    fn = _ctx.Module.GetNamedFunction(symbol);
                    if (fn.Handle == IntPtr.Zero)
                    {
                        fn = _ctx.Module.AddFunction(symbol, slot.Method.Signature.Type);
                    }
                }
                fnPtrs[i] = fn;
            }
            vtableGlobal.Initializer = LLVMValueRef.CreateConstArray(ptrType, fnPtrs);
        }
        else
        {
            vtableGlobal.Initializer = LLVMValueRef.CreateConstNull(vtableArrayType);
        }
        vTable.SetGlobal(vtableGlobal, vtableArrayType);
    }
}
