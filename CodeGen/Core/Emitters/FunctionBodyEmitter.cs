#nullable enable
using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen.Statements;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public class FunctionBodyEmitter(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public void Emit(
        LLVMValueRef function,
        BifyType returnType,
        IReadOnlyList<ParameterInfo> parameters,
        AstNode? body,
        bool isMethod = false,
        bool isDestructor = false,
        ClassType? declaringClass = null)
    {
        var entry = function.AppendBasicBlock("entry");
        _ctx.Builder.PositionAtEnd(entry);
        _ctx.FunctionEntry = entry;
        _ctx.FunctionReturnType = returnType;

        using (_ctx.EnterScope())
        {
            BindParameters(function, parameters, isMethod);

            if (body is AstBlock block)
            {
                new StatementDispatcher(_ctx).EmitBlock(block);
            }

            if (isDestructor && declaringClass != null)
            {
                EmitDestructorFieldCleanups(function, declaringClass);
            }
        }

        if (returnType.Type.Kind == LLVMTypeKind.LLVMVoidTypeKind &&
            _ctx.Builder.InsertBlock.Terminator.Handle == IntPtr.Zero)
        {
            _ctx.Builder.BuildRetVoid();
        }
    }

    private void EmitDestructorFieldCleanups(LLVMValueRef function, ClassType declaringClass)
    {
        if (_ctx.Builder.InsertBlock.Terminator.Handle != IntPtr.Zero)
            return;

        var thisVal = function.GetParam(0);
        foreach (var field in declaringClass.Fields.Values)
        {
            // Inherited fields are released by the chained parent destructor.
            if (field.Type is not ClassType || field.DeclaringClass != declaringClass.Name)
                continue;

            var fieldPtr = MemberResolver.ResolveFieldPtr(_ctx, declaringClass, thisVal, field);
            var fieldVal = _ctx.Builder.BuildLoad2(field.Type.Type, fieldPtr, field.Name);
            if (field.IsWeak)
                _ctx.HeapControl.BuildReleaseWeak(fieldVal);
            else
                _ctx.HeapControl.BuildReleaseStrong(fieldVal);
        }

        EmitParentDestructorCall(declaringClass, thisVal);
    }

    private void EmitParentDestructorCall(ClassType declaringClass, LLVMValueRef thisVal)
    {
        if (declaringClass.Parent == null)
            return;

        var parentDtor = _ctx.Module.GetNamedFunction($"{declaringClass.Parent.Name}_destructor");
        if (parentDtor.Handle == IntPtr.Zero)
            return;

        var dtorType = LLVMTypeRef.CreateFunction(_ctx.Context.VoidType, [thisVal.TypeOf]);
        _ctx.Builder.BuildCall2(dtorType, parentDtor, [thisVal]);
    }

    private void BindParameters(LLVMValueRef function, IReadOnlyList<ParameterInfo> parameters, bool isMethod)
    {
        for (uint i = 0; i < parameters.Count; i++)
        {
            var param = parameters[(int)i];
            var alloca = _ctx.Builder.BuildAlloca(param.Type.Type, param.Name);
            var paramVal = function.GetParam(i);
            _ctx.Builder.BuildStore(paramVal, alloca);
            
            
            bool isThisParam = isMethod && i == 0;

            if (param.Type is ClassType && !isThisParam)
            {
                _ctx.HeapControl.BuildRetainStrong(paramVal);
            }

            _ctx.CurrentScope.Define(param.Name, alloca, param.Type, param.IsConst);
        }
    }
}