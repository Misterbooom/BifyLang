#nullable enable
using System;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Expressions;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Statements;

public class VariableDeclGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public void EmitVariable(AstVarDecl varDecl)
    {
        string varName = varDecl.AssignmentNode.Left.Token.Value;
        bool isVar = varDecl.Type is AstIdentifier id && id.Name == "var";
        var expressionDispatcher = new ExpressionDispatcher(_ctx);

        BifyType varType;
        TypedValue? initialValue = null;

        if (isVar)
        {
            if (varDecl.AssignmentNode.Right == null)
            {
                new BifyTypeError("Implicitly-typed variables ('var') must be initialized.").Throw();
            }

            initialValue = expressionDispatcher.EmitExpression(varDecl.AssignmentNode.Right);
            if (initialValue.Value.Type.Name == "void")
            {
                new BifyTypeError("Cannot declare a variable of type 'void'.").Throw();
            }

            varType = initialValue.Value.Type;
        }
        else
        {
            varType = TypeResolver.Resolve(varDecl.Type, _ctx);

            if (varDecl.AssignmentNode.Right != null)
            {
                var rawValue = expressionDispatcher.EmitExpression(varDecl.AssignmentNode.Right);
                initialValue = TypeCaster.ImplicitCast(_ctx, rawValue, varType);
            }
        }

        var cachedBlock = _ctx.Builder.InsertBlock;
        PositionBeforeTerminator(_ctx.FunctionEntry);
        var alloca = _ctx.Builder.BuildAlloca(varType.Type, varName);

        if (cachedBlock.Handle != IntPtr.Zero)
        {
            _ctx.Builder.PositionAtEnd(cachedBlock);
        }

        if (!FlagChecker.CheckVariableFlags(
                varDecl.Flag.Flags.Select(i => i.Token.Value),
                out var error)
           )
        {
            new BifySyntaxError(error).Throw();
            return;
        }

        if (varDecl.AssignmentNode.Right is AstNew && varDecl.Flag.HasFlag("weak"))
        {
            new BifyTypeError("Can't use weak flag with new statement").Throw();
            return;
        }

        bool isWeak = varDecl.Flag != null && varDecl.Flag.HasFlag("weak");
        bool isConst = varDecl.Flag != null && varDecl.Flag.HasFlag("const");

        if (initialValue != null)
        {
            var init = initialValue.Value;
            if (varType is ClassType)
            {
                if (!isWeak && !init.IsWeak)
                {
                    if (!init.IsOwned)
                        _ctx.HeapControl.BuildRetainStrong(init.Value);
                }
                else if (isWeak && !init.IsWeak)
                {
                    _ctx.HeapControl.BuildRetainWeak(init.Value);
                    if (init.IsOwned)
                        _ctx.HeapControl.BuildReleaseStrong(init.Value);
                }
                else if (isWeak && init.IsWeak)
                {
                    _ctx.HeapControl.BuildRetainWeak(init.Value);
                }
                else
                {
                    var upgraded = _ctx.HeapControl.BuildUpgradeWeak(init.Value);
                    init = new TypedValue(upgraded, init.Type, false, true);
                }
            }

            _ctx.Builder.BuildStore(init.Value, alloca);
        }
        else if (varType is ClassType)
        {
            _ctx.Builder.BuildStore(LLVMValueRef.CreateConstNull(varType.Type), alloca);
        }

        _ctx.CurrentScope.Define(varName, alloca, varType, isConst, isWeak);
    }

    private void PositionBeforeTerminator(LLVMBasicBlockRef block)
    {
        unsafe
        {
            if (block.Handle == IntPtr.Zero)
            {
                throw new ArgumentException("Block handle is invalid.");
            }

            var terminator = LLVM.GetBasicBlockTerminator(block);
            if (terminator != null)
            {
                _ctx.Builder.PositionBefore(terminator);
            }
            else
            {
                _ctx.Builder.PositionAtEnd(block);
            }
        }
    }
}