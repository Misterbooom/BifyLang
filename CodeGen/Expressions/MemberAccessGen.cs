#nullable enable
using System;
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
        if (TryEmitModuleMember(memberAccess, out var moduleMember))
            return moduleMember;

        var (target, classType, memberName) = ResolveTarget(memberAccess);

        if (classType.Fields.TryGetValue(memberName, out var field))
        {
            var fieldPtr = ResolveCheckedFieldPtr(target, classType, field);
            var loaded = _ctx.Builder.BuildLoad2(field.Type.Type, fieldPtr, field.Name);
            return new TypedValue(loaded, field.Type, field.IsWeak);
        }

        if (classType.Methods.TryGetValue(memberName, out var method))
            return EmitMethodValue(classType, method);

        return new BifyAttributeError($"Type '{classType.Name}' has no member '{memberName}'").Throw<TypedValue>();
    }

    public (LLVMValueRef Pointer, ClassField Field, ClassType Class) ResolveMember(AstMemberAccess memberAccess)
    {
        var (target, classType, memberName) = ResolveTarget(memberAccess);

        if (classType.Fields.TryGetValue(memberName, out var field))
            return (ResolveCheckedFieldPtr(target, classType, field), field, classType);

        if (classType.Methods.ContainsKey(memberName))
            return new BifyTypeError($"Cannot assign to method '{memberName}'").Throw<(LLVMValueRef, ClassField, ClassType)>();

        return new BifyAttributeError($"Type '{classType.Name}' has no member '{memberName}'").Throw<(LLVMValueRef, ClassField, ClassType)>();
    }

    private bool TryEmitModuleMember(AstMemberAccess memberAccess, out TypedValue result)
    {
        result = default;
        if (memberAccess.Left is not AstIdentifier modId ||
            !_ctx.ModulesMap.TryGetValue(modId.Name, out var moduleType))
            return false;

        string memberName = ExpectMemberName(memberAccess);
        if (!moduleType.ModuleContext.CurrentScope.TryLookup(memberName, out var member))
        {
            result = new BifyAttributeError($"Module '{modId.Name}' has no member '{memberName}'").Throw<TypedValue>();
            return true;
        }

        result = new TypedValue(member.Pointer, member.Type);
        return true;
    }

    private (TypedValue Target, ClassType Class, string MemberName) ResolveTarget(AstMemberAccess memberAccess)
    {
        var target = new ExpressionDispatcher(_ctx).EmitExpression(memberAccess.Left);
        if (target.Type is not ClassType classType)
            return new BifyTypeError($"Cannot access member on non-class type '{target.Type.Name}'").Throw<(TypedValue, ClassType, string)>();

        string memberName = ExpectMemberName(memberAccess);
        if (target.IsWeak)
        {
            var upgraded = _ctx.HeapControl.BuildUpgradeWeak(target.Value);
            target = new TypedValue(upgraded, target.Type, false);
            _ctx.HeapControl.TrackTemp(target);
        }

        return (target, classType, memberName);
    }

    private LLVMValueRef ResolveCheckedFieldPtr(TypedValue target, ClassType classType, ClassField field)
    {
        MemberResolver.CheckAccess(_ctx, field.DeclaringClass ?? classType, field.AccessLevel, field.Name);
        return MemberResolver.ResolveFieldPtr(_ctx, classType, target.Value, field);
    }

    private TypedValue EmitMethodValue(ClassType classType, ClassMethod method)
    {
        MemberResolver.CheckAccess(_ctx, method.DeclaringClass ?? classType, method.AccessLevel, method.Name);

        var fn = method.FunctionValue;
        if (fn.Handle == IntPtr.Zero)
        {
            string symbol = method.Signature is CallableType ct
                ? ct.FullName
                : NameMangler.GenerateMethodName(_ctx.ModuleContext.Name, classType.Name, method.Name);
            fn = _ctx.Module.GetNamedFunction(symbol);
        }

        return new TypedValue(fn, method.Signature, false);
    }

    private static string ExpectMemberName(AstMemberAccess memberAccess) =>
        memberAccess.Right is AstIdentifier id
            ? id.Name
            : new BifySyntaxError("Expected member identifier").Throw<string>();
}