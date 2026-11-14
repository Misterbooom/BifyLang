using System;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class LiteralGen
{
    public static TypedValue EmitInteger(CodeGenContext ctx, AstInteger node)
    {
        BifyType type = BifyType.CreateInt(ctx);
        ulong rawValue = unchecked((ulong)Convert.ToInt64(node.Value));

        return new TypedValue(
            LLVMValueRef.CreateConstInt(type.Type, rawValue, true),
            type
        );
    }

    public static TypedValue EmitFloat(CodeGenContext ctx, AstFloat node)
    {
        BifyType type = BifyType.CreateFloat(ctx);
        
        return new TypedValue(LLVMValueRef.CreateConstReal(type.Type, Convert.ToDouble(node.Value)), type);
    }

    public static TypedValue EmitBool(CodeGenContext ctx, AstBoolean node)
    {
        BifyType type = BifyType.CreateBool(ctx);
        
        return new TypedValue(LLVMValueRef.CreateConstInt(type.Type, (bool)node.Value ? 1UL : 0UL, false), type);
    }

    public static TypedValue EmitNull(CodeGenContext ctx, AstNull node)
    {
        BifyType type = BifyType.CreateNull(ctx);
        
        return new TypedValue(LLVMValueRef.CreateConstPointerNull(type.Type), type);
    }

    public static TypedValue EmitString(CodeGenContext ctx, AstString node)
    {
        BifyType type = BifyType.CreateString(ctx);
        string text = (string)node.Value;
        var constStr = ctx.Context.GetConstString(text.AsSpan(), false);
        var globalVar = ctx.Module.AddGlobal(constStr.TypeOf, ".str");
        globalVar.Initializer = constStr;
        unsafe
        {
            LLVM.SetGlobalConstant(globalVar, 1);
        }
        globalVar.Linkage = LLVMLinkage.LLVMPrivateLinkage;
        return new TypedValue(globalVar, type);
    }
}