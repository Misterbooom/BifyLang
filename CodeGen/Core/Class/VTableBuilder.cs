#nullable enable
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Core;

public static class VTableBuilder
{
    public static VTable Inherit(ClassType? parent)
    {
        return parent?.VTable != null ? parent.VTable.Clone() : new VTable();
    }

    public static int Apply(VTable vtable, ClassMethod method, ClassType? parent)
    {
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
}
