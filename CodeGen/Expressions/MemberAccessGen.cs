#nullable enable
using System;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class MemberAccessGen(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public TypedValue EmitMemberAccess(AstMemberAccess memberAccess)
    {
        if (TryEmitModuleMember(memberAccess, out var moduleMember))
            return moduleMember;

        var (target, classType, memberName) = ResolveTarget(memberAccess);

        if (classType.Fields.TryGetValue(memberName, out var field))
        {
            if (field.IsPoison)
                AbortPoison();

            var fieldPtr = ResolveCheckedFieldPtr(target, classType, field);
            var loaded = _ctx.Builder.BuildLoad2(field.Type.Type, fieldPtr, field.Name);
            return new TypedValue(loaded, field.Type, field.IsWeak);
        }

        if (classType.Methods.TryGetValue(memberName, out var method))
        {
            if (method.IsPoison)
                AbortPoison();

            return EmitMethodValue(classType, method);
        }

        return Fail<TypedValue>($"Type '{classType.Name}' has no member '{memberName}'", DiagnosticKind.Attribute, memberAccess);
    }

    public (LLVMValueRef Pointer, ClassField Field, ClassType Class) ResolveMember(AstMemberAccess memberAccess)
    {
        var (target, classType, memberName) = ResolveTarget(memberAccess);

        if (classType.Fields.TryGetValue(memberName, out var field))
        {
            if (field.IsPoison)
                AbortPoison();

            return (ResolveCheckedFieldPtr(target, classType, field), field, classType);
        }

        if (classType.Methods.ContainsKey(memberName))
            return Fail<(LLVMValueRef, ClassField, ClassType)>($"Cannot assign to method '{memberName}'", DiagnosticKind.Type, memberAccess);

        return Fail<(LLVMValueRef, ClassField, ClassType)>($"Type '{classType.Name}' has no member '{memberName}'", DiagnosticKind.Attribute, memberAccess);
    }

    private bool TryEmitModuleMember(AstMemberAccess memberAccess, out TypedValue result)
    {
        result = default;
        if (memberAccess.Left is not AstIdentifier modId ||
            !_ctx.ModulesMap.TryGetValue(modId.Name, out var moduleType))
            return false;

        string memberName = ExpectMemberName(memberAccess);
        if (!moduleType.ModuleContext.CurrentScope.TryLookup(memberName, out var member))
            return Fail<bool>($"Module '{modId.Name}' has no member '{memberName}'", DiagnosticKind.Attribute, memberAccess);

        if (member.IsPoison)
            AbortPoison();

        result = new TypedValue(member.Pointer, member.Type);
        return true;
    }

    private (TypedValue Target, ClassType Class, string MemberName) ResolveTarget(AstMemberAccess memberAccess)
    {
        var target = new ExpressionDispatcher(_ctx).EmitExpression(memberAccess.Left);
        if (target.Type is not ClassType classType)
            return Fail<(TypedValue, ClassType, string)>($"Cannot access member on non-class type '{target.Type.Name}'", DiagnosticKind.Type, memberAccess);

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

    private string ExpectMemberName(AstMemberAccess memberAccess) =>
        memberAccess.Right is AstIdentifier id
            ? id.Name
            : Fail<string>("Expected member identifier", DiagnosticKind.Syntax, memberAccess);
}