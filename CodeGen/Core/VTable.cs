#nullable enable
using System.Collections.Generic;
using BifyLang.CodeGen.Types;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public sealed class VTableSlot
{
    public required string MethodName { get; init; }
    public required ClassMethod Method { get; set; }
    public int Index { get; init; }
}

public class VTable
{
    private readonly List<VTableSlot> _slots = new();
    public LLVMValueRef GlobalValue { get; private set; }
    public LLVMTypeRef GlobalType { get; private set; }

    public IReadOnlyList<VTableSlot> Slots => _slots;

    public bool TryFind(string methodName, out VTableSlot? slot)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].MethodName == methodName)
            {
                slot = _slots[i];
                return true;
            }
        }
        slot = null;
        return false;
    }

    public void Replace(int index, ClassMethod method)
    {
        _slots[index].Method = method;
    }

    public int AddMethod(ClassMethod method)
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

    public VTable Clone()
    {
        var copy = new VTable();
        for (int i = 0; i < _slots.Count; i++)
        {
            copy._slots.Add(new VTableSlot
            {
                MethodName = _slots[i].MethodName,
                Method = _slots[i].Method,
                Index = _slots[i].Index
            });
        }
        return copy;
    }

    public void SetGlobal(LLVMValueRef valueRef, LLVMTypeRef typeRef)
    {
        GlobalValue = valueRef;
        GlobalType = typeRef;
    }

  
}