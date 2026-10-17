using System.Collections.Generic;
using BifyLang.CodeGen.Declarations.ClassHandler;

namespace BifyLang.CodeGen.Core;
public sealed class VTableSlot
{
    public required string MethodName { get; init; }
    public required ClassField Method { get; set; }
    public int Index { get; init; }
}
public class VTable
{
    private readonly List<VTableSlot> _slots = new();

    public IReadOnlyList<VTableSlot> Slots => _slots;

    public int AddMethod(ClassField method)
    {
        var existing = _slots.FindIndex(x => x.MethodName == method.Name);

        if (existing >= 0)
        {
            _slots[existing].Method = method;
            return existing;
        }

        var slot = _slots.Count;

        _slots.Add(new VTableSlot
        {
            MethodName = method.Name,
            Method = method,
            Index = slot
        });

        return slot;
    }
}