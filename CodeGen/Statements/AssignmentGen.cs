#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Expressions;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Statements;

public class AssignmentGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public void EmitAssign(AstAssignmentOperator assignmentOp)
    {
        var (pointer, type, name, isConstant, isWeak) = ResolveTarget(assignmentOp.IdentifierNode);
        if (isConstant)
        {
            new BifyTypeError("Assigning to constant variable!").Throw();
        }

        TypedValue value = new ExpressionDispatcher(_ctx).EmitExpression(assignmentOp.ValueNode);

        TokenType? binOp = assignmentOp.Token.Type switch
        {
            TokenType.ASSIGN => null,
            TokenType.ADDE => TokenType.ADD,
            TokenType.SUBE => TokenType.SUB,
            TokenType.MULE => TokenType.MUL,
            TokenType.DIVE or TokenType.FLOORDIVE => TokenType.DIV,
            _ => new BifySyntaxError($"Unsupported assignment operator: {assignmentOp.Token.Value}").Throw<TokenType?>()
        };

        if (binOp.HasValue)
        {
            var currentVal = new TypedValue(_ctx.Builder.BuildLoad2(type.Type, pointer, name), type);
            value = BinaryOpGen.EmitBinary(_ctx, binOp.Value, currentVal, value);
        }
        else if (value.Type is ClassType rhs && type is ClassType lhs)
        {
            var b = value;
            var oldVal = _ctx.Builder.BuildLoad2(type.Type, pointer, "old_" + name);
            bool aWeak = isWeak;
            bool bWeak = value.IsWeak;

            if (!aWeak && !bWeak)
            {
                if (!b.IsOwned)
                    _ctx.HeapControl.BuildRetainStrong(b.Value);
                _ctx.HeapControl.BuildReleaseStrong(oldVal);
            }
            else if (aWeak && !bWeak)
            {
                _ctx.HeapControl.BuildRetainWeak(b.Value);
                if (b.IsOwned)
                    _ctx.HeapControl.BuildReleaseStrong(b.Value);
                _ctx.HeapControl.BuildReleaseWeak(oldVal);
            }
            else if (aWeak && bWeak)
            {
                _ctx.HeapControl.BuildRetainWeak(b.Value);
                _ctx.HeapControl.BuildReleaseWeak(oldVal);
            }
            else
            {
                var upgraded = _ctx.HeapControl.BuildUpgradeWeak(b.Value);
                _ctx.HeapControl.BuildReleaseStrong(oldVal);
                value = new TypedValue(upgraded, b.Type, false, true);
            }
        }

        TypedValue casted = (binOp.HasValue && type.Name == "char" && value.Type.Name == "int")
            ? TypeCaster.ExplicitCast(_ctx, value, type)
            : TypeCaster.ImplicitCast(_ctx, value, type);

        _ctx.Builder.BuildStore(casted.Value, pointer);
    }

    private (LLVMValueRef Pointer, BifyType Type, string Name, bool IsConstant, bool isWeak) ResolveTarget(AstNode node)
    {
        if (node is AstIdentifier id)
        {
            if (_ctx.CurrentScope.TryLookup(id.Name, out var symbol))
                return (symbol.Pointer, symbol.Type, symbol.Name, symbol.IsConstant, symbol.IsWeak);

            return new BifyNameError($"Variable '{id.Name}' is not defined").Throw<(LLVMValueRef, BifyType, string, bool, bool)>();
        }

        return new BifyTypeError($"Cannot assign to {node.Token.Value}").Throw<(LLVMValueRef, BifyType, string, bool, bool)>();
    }
}