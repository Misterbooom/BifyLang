#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class MemberAccessGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public TypedValue EmitMemberAccess(AstMemberAccess memberAccess)
    {
        
        if (memberAccess.Left is AstIdentifier identifier && _ctx.ModulesMap.TryGetValue(identifier.Name, out var moduleType))
        {
            if (memberAccess.Right is not AstIdentifier key)
            {
                return new BifyAttributeError("Expected member identifier").Throw<TypedValue>();
            }

            if (moduleType.ModuleContext.CurrentScope.TryLookup(key.Name, out var member))
            {
                return new TypedValue(member.Pointer, member.Type);
            }
            return new BifyAttributeError($"Module '{identifier.Name}' has no member '{key.Name}'").Throw<TypedValue>();
        }
        var (target, classType, memberName) = EmitTarget(memberAccess);

        if (classType.Fields.TryGetValue(memberName, out var field))
        {
            MemberResolver.CheckAccess(_ctx, classType, field.AccessLevel, field.Name);
            var fieldPtr = MemberResolver.ResolveFieldPtr(_ctx, classType, target.Value, field);
            var loaded = _ctx.Builder.BuildLoad2(field.Type.Type, fieldPtr, field.Name);
            return new TypedValue(loaded, field.Type, field.IsWeak);
        }

        if (classType.Methods.TryGetValue(memberName, out var method))
        {
            MemberResolver.CheckAccess(_ctx, classType, method.AccessLevel, method.Name);
            string symbol = NameMangler.GenerateMethodName(_ctx.ModuleContext.Name, classType.Name, method.Name);
            var fn = _ctx.Module.GetNamedFunction(symbol);
            BifyDebug.Log("METHOD: " + method.Signature.Name);
            return new TypedValue(fn, method.Signature, false);
        }

        return new BifyAttributeError($"Type '{classType.Name}' has no member '{memberName}'").Throw<TypedValue>();
    }

    public (LLVMValueRef Pointer, ClassField Field, ClassType Class) ResolveMember(AstMemberAccess memberAccess)
    {
        var (target, classType, memberName) = EmitTarget(memberAccess);

        if (classType.Fields.TryGetValue(memberName, out var field))
        {
            MemberResolver.CheckAccess(_ctx, classType, field.AccessLevel, field.Name);
            var fieldPtr = MemberResolver.ResolveFieldPtr(_ctx, classType, target.Value, field);
            return (fieldPtr, field, classType);
        }

        if (classType.Methods.ContainsKey(memberName))
            return new BifyTypeError($"Cannot assign to method '{memberName}'").Throw<(LLVMValueRef, ClassField, ClassType)>();

        return new BifyAttributeError($"Type '{classType.Name}' has no member '{memberName}'").Throw<(LLVMValueRef, ClassField, ClassType)>();
    }


    private (TypedValue Target, ClassType Class, string MemberName) EmitTarget(AstMemberAccess memberAccess)
    {

        
        var target = new ExpressionDispatcher(_ctx).EmitExpression(memberAccess.Left);
        if (target.Type is not ClassType classType)
            return new BifyTypeError($"Cannot access member on non-class type '{target.Type.Name}'").Throw<(TypedValue, ClassType, string)>();

        if (memberAccess.Right is not AstIdentifier memberId)
            return new BifySyntaxError("Expected member identifier").Throw<(TypedValue, ClassType, string)>();

        if (target.IsWeak)
        {
            var upgraded = _ctx.HeapControl.BuildUpgradeWeak(target.Value);
            target = new TypedValue(upgraded, target.Type, false);
            _ctx.HeapControl.TempRefs.Add(target);
        }

        return (target, classType, memberId.Name);
    }
}