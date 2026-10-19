#nullable enable
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
            if (ctx.CurrentScope.TryLookup("this", out var thisSym) &&
                thisSym.Type is ClassType ct &&
                ct.Fields.TryGetValue(name, out var field) &&
                !field.IsMethod)
            {
                MemberAccessGen.CheckAccess(ctx, ct, field);
                var thisVal = ctx.Builder.BuildLoad2(thisSym.Type.Type, thisSym.Pointer, "this");
                var fieldPtr = ctx.Builder.BuildStructGEP2(ct.StructType, thisVal, (uint)field.Index, field.Name);
                var val = ctx.Builder.BuildLoad2(field.Type.Type, fieldPtr, field.Name);
                return new TypedValue(val, field.Type, field.IsWeak);
            }

            new BifyNameError($"Variable '{name}' is not defined").Throw();
        }
        if (symbol.Type.Type.Kind == LLVMTypeKind.LLVMFunctionTypeKind)
        {
            return new TypedValue(symbol.Pointer, symbol.Type);
        }

        var loadedValue = ctx.Builder.BuildLoad2(symbol.Type.Type, symbol.Pointer, name);
        return new TypedValue(loadedValue, symbol.Type, symbol.IsWeak);
    }
}