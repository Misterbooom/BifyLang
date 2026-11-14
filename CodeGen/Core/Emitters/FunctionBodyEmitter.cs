#nullable enable
using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Statements;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Emitters;

public class FunctionBodyEmitter(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public void Emit(
        LLVMValueRef function,
        BifyType returnType,
        IReadOnlyList<ParameterInfo> parameters,
        AstNode? body,
        bool isMethod = false,
        bool isConstructor = false,
        bool isDestructor = false,
        ClassType? declaringClass = null)
    {
        LLVMBasicBlockRef? dtorCleanup = null;

        var entry = function.AppendBasicBlock("entry");
        if (isDestructor)
            dtorCleanup = function.AppendBasicBlock("dtorCleanup");
        _ctx.Builder.PositionAtEnd(entry);
        _ctx.FunctionEntry = entry;
        _ctx.FunctionReturnType = returnType;

        using (_ctx.EnterScope())
        {
            _ctx.CurrentScope.FunctionName = function.Name;
            BindParameters(function, parameters, isMethod);
            _ctx.CurrentScope.IsDestructor = isDestructor;
            if (isConstructor && declaringClass != null)
            {
                if (!_ctx.CurrentScope.TryLookup("this", out var thisSym))
                    throw new InvalidOperationException("ThisSym fucked up.");
                var loaded = _ctx.Builder.BuildLoad2(thisSym.Type.Type, thisSym.Pointer, "this_loaded");
                ClassBuilder.InitFields(_ctx, declaringClass, loaded);
            }
            if (body is AstBlock block)
            {
                new StatementDispatcher(_ctx).EmitBlock(block);
            }
        }

        if (isDestructor && dtorCleanup.HasValue && _ctx.Builder.InsertBlock.Terminator.Handle == IntPtr.Zero)
        {
            _ctx.Builder.BuildBr(dtorCleanup.Value);
        }
        if (isDestructor && declaringClass != null)
        {
            EmitDestructorFieldCleanups(function, declaringClass);
        }
        else if (returnType.Type.Kind == LLVMTypeKind.LLVMVoidTypeKind &&
            _ctx.Builder.InsertBlock.Terminator.Handle == IntPtr.Zero)
        {
            _ctx.Builder.BuildRetVoid();
        }
    }

    private void EmitDestructorFieldCleanups(LLVMValueRef function, ClassType declaringClass)
    {

        var dtorCleanup = function.GetBasicBlock("dtorCleanup");
        _ctx.Builder.PositionAtEnd(dtorCleanup);
        var thisVal = function.GetParam(0);
        foreach (var field in declaringClass.Fields.Values)
        {
            if (field.IsPoison || field.Type is not ClassType || field.DeclaringClass.FullName != declaringClass.FullName)
                continue;

            var fieldPtr = MemberResolver.ResolveFieldPtr(_ctx, declaringClass, thisVal, field);
            var fieldVal = _ctx.Builder.BuildLoad2(field.Type.Type, fieldPtr, field.Name);
            if (field.IsWeak)
                _ctx.HeapControl.BuildReleaseWeak(fieldVal);
            else
                _ctx.HeapControl.BuildReleaseStrong(fieldVal);
        }

        EmitParentDestructorCall(declaringClass, thisVal);
        _ctx.Builder.BuildRetVoid();
    }


    private void EmitParentDestructorCall(ClassType declaringClass, LLVMValueRef thisVal)
    {
        if (declaringClass.Parent == null)
            return;

        var parentDtor = _ctx.Module.GetNamedFunction(NameMangler.GenerateMethodName(declaringClass.Parent.ModuleName, declaringClass.Parent.Name, "destructor"));
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
