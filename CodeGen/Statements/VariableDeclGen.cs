#nullable enable
using System;
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

        if (initialValue != null)
        {
            _ctx.Builder.BuildStore(initialValue.Value.Value, alloca);
        }

        bool isConst = varDecl.Flag != null && varDecl.Flag.HasFlag("const");
        _ctx.CurrentScope.Define(varName, alloca, varType, isConst);
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