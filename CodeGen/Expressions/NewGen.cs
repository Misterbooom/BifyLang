#nullable enable
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class NewGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public TypedValue EmitNew(AstNew astNew)
    {
        if (astNew.ValueNode is not AstIdentifier id)
            return new BifySyntaxError("Expected class identifier after 'new'").Throw<TypedValue>();

        var bifyType = _ctx.TypeRegistry.GetBifyType(id.Name);
        if (bifyType is not ClassType classType)
            return new BifyTypeError($"'{id.Name}' is not a class").Throw<TypedValue>();

        var instancePtr = _ctx.Builder.BuildMalloc(classType.StructType, $"{classType.Name}_inst");

        var vptrSlot = _ctx.Builder.BuildStructGEP2(classType.StructType, instancePtr, 0, "vptr_slot");
        _ctx.Builder.BuildStore(classType.VTableGlobal, vptrSlot);

        var strongCountSlot = _ctx.Builder.BuildStructGEP2(classType.StructType, instancePtr, 1,"strong_count");
        var weakCountSlot = _ctx.Builder.BuildStructGEP2(classType.StructType, instancePtr, 2,"weak_count");
        
        _ctx.Builder.BuildStore(LLVMValueRef.CreateConstInt(_ctx.Context.Int32Type, 1), strongCountSlot);
        _ctx.Builder.BuildStore(LLVMValueRef.CreateConstInt(_ctx.Context.Int32Type, 1), weakCountSlot);
        

        foreach (var field in classType.Fields.Values.Where(f => !f.IsMethod).OrderBy(f => f.Index))
        {
            var fieldPtr = _ctx.Builder.BuildStructGEP2(classType.StructType, instancePtr, (uint)field.Index, field.Name);
            if (field.ValueNode != null)
            {
                var initVal = new ExpressionDispatcher(_ctx).EmitExpression(field.ValueNode);
                initVal = TypeCaster.ImplicitCast(_ctx, initVal, field.Type);
                _ctx.Builder.BuildStore(initVal.Value, fieldPtr);
            }
            else
            {
                _ctx.Builder.BuildStore(LLVMValueRef.CreateConstNull(field.Type.Type), fieldPtr);
            }
        }

        if (classType.Fields.TryGetValue("constructor", out var ctor) && ctor.IsMethod)
        {
            MemberAccessGen.CheckAccess(_ctx, classType, ctor);
            var args = CallGen.ResolveArgs(_ctx, astNew.ArgumentsNode, ctor.Type.Type, instancePtr);
            _ctx.Builder.BuildCall2(ctor.Type.Type, ctor.Value, [.. args]);
        }
        else if (astNew.ArgumentsNode != null)
        {
            new BifyTypeError($"Class '{classType.Name}' has no constructor matching arguments").Throw();
        }

        return new TypedValue(instancePtr, classType, false, true);
    }
}