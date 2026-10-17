#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Declarations.ClassHandler;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public static class MemberAccessGen
{
    public static TypedValue EmitMemberAccess(CodeGenContext ctx, AstMemberAccess memberAccess)
    {
        var target = new ExpressionDispatcher(ctx).EmitExpression(memberAccess.Left);
        if (target.Type is not ClassType classType)
            return new BifyTypeError($"Cannot access member on non-class type '{target.Type.Name}'").Throw<TypedValue>();

        if (memberAccess.Right is not AstIdentifier memberId)
            return new BifySyntaxError("Expected member identifier").Throw<TypedValue>();

        if (!classType.Fields.TryGetValue(memberId.Name, out var field))
            return new BifyAttributeError($"Type '{classType.Name}' has no member '{memberId.Name}'").Throw<TypedValue>();

        CheckAccess(ctx, classType, field);

        if (field.IsMethod)
            return new TypedValue(field.Value, field.Type);

        var fieldPtr = ctx.Builder.BuildStructGEP2(classType.StructType, target.Value, (uint)field.Index, field.Name);
        var loaded = ctx.Builder.BuildLoad2(field.Type.Type, fieldPtr, field.Name);
        return new TypedValue(loaded, field.Type);
    }

    public static (LLVMValueRef Pointer, ClassField Field, ClassType Class) ResolveMember(CodeGenContext ctx, AstMemberAccess memberAccess)
    {
        var target = new ExpressionDispatcher(ctx).EmitExpression(memberAccess.Left);
        if (target.Type is not ClassType classType)
            return new BifyTypeError($"Cannot access member on non-class type '{target.Type.Name}'").Throw<(LLVMValueRef, ClassField, ClassType)>();

        if (memberAccess.Right is not AstIdentifier memberId)
            return new BifySyntaxError("Expected member identifier").Throw<(LLVMValueRef, ClassField, ClassType)>();

        if (!classType.Fields.TryGetValue(memberId.Name, out var field))
            return new BifyAttributeError($"Type '{classType.Name}' has no member '{memberId.Name}'").Throw<(LLVMValueRef, ClassField, ClassType)>();

        CheckAccess(ctx, classType, field);

        if (field.IsMethod)
            return new BifyTypeError($"Cannot assign to method '{field.Name}'").Throw<(LLVMValueRef, ClassField, ClassType)>();

        var fieldPtr = ctx.Builder.BuildStructGEP2(classType.StructType, target.Value, (uint)field.Index, field.Name);
        return (fieldPtr, field, classType);
    }

    public static void CheckAccess(CodeGenContext ctx, ClassType classType, ClassField field)
    {
        string declClass = string.IsNullOrEmpty(field.DeclaringClass) ? classType.Name : field.DeclaringClass;
        if (field.AccessLevel is AccessLevel.PRIVATE)
        {
            if (!ctx.CurrentScope.TryLookup("this", out var thisSym) ||
                thisSym.Type is not ClassType ct ||
                ct.Name != declClass)
            {
                new BifyAttributeError($"Cannot access private member '{field.Name}' of class '{declClass}'").Throw();
            }
        }
        else if (field.AccessLevel is AccessLevel.PROTECTED)
        {
            bool allowed = false;
            if (ctx.CurrentScope.TryLookup("this", out var thisSym) && thisSym.Type is ClassType ct)
            {
                ClassType? cur = ct;
                while (cur != null)
                {
                    if (cur.Name == declClass)
                    {
                        allowed = true;
                        break;
                    }
                    cur = cur.Parent;
                }
            }

            if (!allowed)
                new BifyAttributeError($"Cannot access protected member '{field.Name}' of class '{declClass}'").Throw();
        }
    }
}
