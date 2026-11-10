using System.Collections.Generic;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public class TypeRegistry
{
    private readonly Dictionary<string, BifyType> _types = new();
    private readonly CodeGenContext _ctx;

    public TypeRegistry(CodeGenContext ctx)
    {
        _ctx = ctx;
        Register("int", BifyType.CreateInt(ctx));
        Register("float", BifyType.CreateFloat(ctx));
        Register("bool", BifyType.CreateBool(ctx));
        Register("void", BifyType.CreateVoid(ctx));
        Register("string", BifyType.CreateString(ctx));
        Register("null", BifyType.CreateNull(ctx));
        Register("double", BifyType.CreateDouble(ctx));
        Register("char", BifyType.CreateChar(ctx));
    }

    public void Register(string name, BifyType type)
    {
        _types[name] = type;
    }

    public bool TryGetType(string name, out BifyType type) =>
        _types.TryGetValue(name, out type);

    public BifyType GetBifyType(string name)
    {
        if (TryGetType(name, out var existing))
            return existing;

        if (name.EndsWith("*"))
        {
            var baseType = GetBifyType(name[..^1]);
            var llvmType = baseType.Type == _ctx.Context.VoidType
                ? LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0)
                : LLVMTypeRef.CreatePointer(baseType.Type, 0);
            var ptrType = new BifyType(name, llvmType);
            _types[name] = ptrType;
            return ptrType;
        }

        if (name.EndsWith("]"))
        {
            int idx = name.LastIndexOf('[');
            string baseName = idx > 0 ? name[..idx] : name;
            var baseType = GetBifyType(baseName);
            var llvmType = LLVMTypeRef.CreatePointer(baseType.Type, 0);
            var arrType = new BifyType(name, llvmType);
            _types[name] = arrType;
            return arrType;
        }

        var customType = new BifyType(name, LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0));
        _types[name] = customType;
        return customType;
    }
}