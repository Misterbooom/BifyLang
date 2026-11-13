#nullable enable
using System;
using System.Linq;
using BifyLang.CodeGen.Expressions;
using BifyLang.CodeGen.Types;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public class ClassBuilder(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public unsafe LLVMValueRef BuildInstance(ClassType classType)
    {
        var ptrType = LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0);
        var vtableGlobal = classType.VTableGlobal.Handle != IntPtr.Zero
            ? classType.VTableGlobal
            : classType.VTable.GlobalValue;

        var vptrCast = _ctx.Builder.BuildBitCast(vtableGlobal, ptrType, "vptr_cast");
        var sizeVal = LLVM.SizeOf(classType.StructType);
        var rawPtr = _ctx.HeapControl.BuildAlloc(sizeVal, vptrCast);
        var instancePtr = _ctx.Builder.BuildBitCast(rawPtr, classType.Type, $"{classType.Name}_inst");

        return instancePtr;
    }

    public static void InitFields(CodeGenContext ctx, ClassType classType, LLVMValueRef instancePtr)
    {
        foreach (var field in classType.Fields.Values.OrderBy(f => f.Index))
        {
            var fieldPtr = MemberResolver.ResolveFieldPtr(ctx, classType, instancePtr, field);

            if (field.ValueNode != null)
            {
                using (ctx.HeapControl.BeginTempScope())
                {
                    
                    var initVal = new ExpressionDispatcher(ctx).EmitExpression(field.ValueNode);
                    initVal = TypeCaster.ImplicitCast(ctx, initVal, field.Type);
                    if (field.Type is ClassType)
                        ctx.HeapControl.BuildArcStoreInit(fieldPtr, initVal, field.IsWeak);
                    else
                        ctx.Builder.BuildStore(initVal.Value, fieldPtr);
                }
            }
            else
            {
                ctx.Builder.BuildStore(LLVMValueRef.CreateConstNull(field.Type.Type), fieldPtr);
            }
        }
    }
}