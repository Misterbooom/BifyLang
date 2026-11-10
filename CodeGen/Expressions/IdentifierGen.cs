#nullable enable
using System;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class IdentifierGen
{
    public static TypedValue EmitIdentifier(CodeGenContext ctx, AstIdentifier identifier)
    {
        string name = identifier.Name;
        if (name == "true")
            return LiteralGen.EmitBool(ctx, new AstBoolean(identifier.Token, true));
        if (name == "false")
            return LiteralGen.EmitBool(ctx, new AstBoolean(identifier.Token, false));
        if (name == "null")
            return LiteralGen.EmitNull(ctx, new AstNull(identifier.Token));

        if (!ctx.CurrentScope.TryLookup(name, out var symbol))
        {
            if (MemberResolver.TryResolveThisField(ctx, name, out var fieldPtr, out var field))
            {
                var loaded = ctx.Builder.BuildLoad2(field.Type.Type, fieldPtr, field.Name);
                return new TypedValue(loaded, field.Type, field.IsWeak);
            }

            new BifyNameError($"Variable '{name}' is not defined").Throw();
            return new TypedValue(default, BifyType.CreateInt(ctx));
        }
        if (symbol.Type.Type.Kind == LLVMTypeKind.LLVMFunctionTypeKind)
        {
            return new TypedValue(symbol.Pointer, symbol.Type);
        }

        var loadedValue = ctx.Builder.BuildLoad2(symbol.Type.Type, symbol.Pointer, name);
        return new TypedValue(loadedValue, symbol.Type, symbol.IsWeak);
    }
}