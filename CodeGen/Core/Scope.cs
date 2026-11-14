#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public readonly struct Symbol
{
    public string Name { get; }
    public LLVMValueRef Pointer { get; } 
    public BifyType Type { get; }         
    public bool IsConstant { get; }      
    public bool IsWeak { get; }

    public Symbol(string name, LLVMValueRef pointer, BifyType type, bool isConstant = false, bool isWeak = false)
    {
        Name = name;
        Pointer = pointer;
        Type = type;
        IsConstant = isConstant;
        IsWeak =  isWeak;
    }
}

public class Scope
{
    public Scope? Parent { get; }

    public bool IsDestructor { get; set; }
    
    private readonly Dictionary<string, Symbol> _symbols = new();

        public Scope(Scope? parent = null)
        {
            Parent = parent;
            IsDestructor = parent?.IsDestructor ?? false;
        }

        public bool Define(string name, LLVMValueRef pointer, BifyType type, bool isConstant = false, bool isWeak = false)
        {
            if (_symbols.ContainsKey(name))
            {
                return false; 
            }

            _symbols[name] = new Symbol(name, pointer, type, isConstant, isWeak);
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

        public void ReleaseLocals(CodeGenContext ctx)
        {
            if (ctx.Builder.InsertBlock.Terminator.Handle != IntPtr.Zero)
                return;
            
            
            foreach (var sym in _symbols.Values.Reverse())
            {
                if (sym.Name != "this" && sym.Type is ClassType)
                {
                    
                    var val = ctx.Builder.BuildLoad2(sym.Type.Type, sym.Pointer, "cleanup_" + sym.Name);
                    if (sym.IsWeak)
                        ctx.HeapControl.BuildReleaseWeak(val);
                    else
                        ctx.HeapControl.BuildReleaseStrong(val);
                }
            }
        }
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            var current = this;
            var depth = 0;

            while (current != null)
            {
                var indent = new string(' ', depth * 2);
                sb.AppendLine($"{indent}Scope (Level {depth}) [{current._symbols.Count} symbol(s)]:");

                if (current._symbols.Count == 0)
                {
                    sb.AppendLine($"{indent}  <empty>");
                }
                else
                {
                    foreach (var sym in current._symbols.Values)
                    {
                        var modifiers = (sym.IsConstant, sym.IsWeak) switch
                        {
                            (true, true) => " [const, weak]",
                            (true, false) => " [const]",
                            (false, true) => " [weak]",
                            _ => ""
                        };

                        sb.AppendLine($"{indent}  - {sym.Name}: {sym.Type.GetType().Name}({sym.Type.Name}){modifiers} (0x{sym.Pointer.Handle:X})");
                    }
                }

                current = current.Parent;
                depth++;
            }

            return sb.ToString().TrimEnd();
        }
    }