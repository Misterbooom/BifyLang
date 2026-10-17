#nullable enable
using System;
using System.Collections.Generic;
using LLVMSharp.Interop;
using BifyLang.CodeGen.Types;

namespace BifyLang.CodeGen.Core
{
    public readonly struct Symbol
    {
        public string Name { get; }
        public LLVMValueRef Pointer { get; } 
        public BifyType Type { get; }         
        public bool IsConstant { get; }      

        public Symbol(string name, LLVMValueRef pointer, BifyType type, bool isConstant = false)
        {
            Name = name;
            Pointer = pointer;
            Type = type;
            IsConstant = isConstant;
        }
    }

    public class Scope
    {
        public Scope? Parent { get; }
        
        private readonly Dictionary<string, Symbol> _symbols = new();

        public Scope(Scope? parent = null)
        {
            Parent = parent;
        }

        public bool Define(string name, LLVMValueRef pointer, BifyType type, bool isConstant = false)
        {
            if (_symbols.ContainsKey(name))
            {
                return false; 
            }

            _symbols[name] = new Symbol(name, pointer, type, isConstant);
            return true;
        }

        public bool TryLookup(string name, out Symbol symbol)
        {
            if (_symbols.TryGetValue(name, out symbol))
            {
                return true;
            }

            if (Parent != null)
            {
                return Parent.TryLookup(name, out symbol);
            }

            symbol = default;
            return false;
        }

        public bool IsDeclaredLocally(string name) => _symbols.ContainsKey(name);
    }
}