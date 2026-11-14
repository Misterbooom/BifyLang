#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Expressions;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Statements;

public class AssignmentGen(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public void EmitAssign(AstAssignmentOperator assignmentOp)
    {
        var (pointer, type, name, isConstant, isWeak) = ResolveTarget(assignmentOp.IdentifierNode);
        if (isConstant)
            Fail("Assigning to constant variable!", DiagnosticKind.Type, assignmentOp);

        TypedValue value = new ExpressionDispatcher(_ctx).EmitExpression(assignmentOp.ValueNode);

        TokenType? binOp = assignmentOp.Token.Type switch
        {
            TokenType.ASSIGN => null,
            TokenType.ADDE => TokenType.ADD,
            TokenType.SUBE => TokenType.SUB,
            TokenType.MULE => TokenType.MUL,
            TokenType.DIVE or TokenType.FLOORDIVE => TokenType.DIV,
            _ => Fail<TokenType?>($"Unsupported assignment operator: {assignmentOp.Token.Value}", DiagnosticKind.Syntax, assignmentOp)
        };

        if (binOp.HasValue)
        {
            var currentVal = new TypedValue(_ctx.Builder.BuildLoad2(type.Type, pointer, name), type);
            value = BinaryOpGen.EmitBinary(_ctx, binOp.Value, currentVal, value);
            StoreAssignedValue(pointer, type, value, isWeak, allowCharFromInt: true);
            return;
        }

        StoreAssignedValue(pointer, type, value, isWeak, allowCharFromInt: false);
    }

    private void StoreAssignedValue(LLVMValueRef pointer, BifyType type, TypedValue value, bool isWeak, bool allowCharFromInt)
    {
        if (value.Type is ClassType || (value.Type.Name == "null" && type is ClassType))
        {
            TypedValue casted = TypeCaster.ImplicitCast(_ctx, value, type);
            _ctx.HeapControl.BuildArcAssign(pointer, casted, isWeak);
            return;
        }

        TypedValue scalar = allowCharFromInt && type.Name == "char" && value.Type.Name == "int"
            ? TypeCaster.ExplicitCast(_ctx, value, type)
            : TypeCaster.ImplicitCast(_ctx, value, type);
        _ctx.Builder.BuildStore(scalar.Value, pointer);
    }

    private (LLVMValueRef Pointer, BifyType Type, string Name, bool IsConstant, bool isWeak) ResolveTarget(AstNode node)
    {
        if (node is AstIdentifier id)
        {
            if (_ctx.CurrentScope.TryLookup(id.Name, out var symbol))
            {
                if (symbol.IsPoison)
                    AbortPoison();

                return (symbol.Pointer, symbol.Type, symbol.Name, symbol.IsConstant, symbol.IsWeak);
            }

            if (MemberResolver.TryResolveThisField(_ctx, id.Name, out var ptr, out var f))
                return (ptr, f.Type, f.Name, f.IsConst, f.IsWeak);

            return Fail<(LLVMValueRef, BifyType, string, bool, bool)>($"Variable '{id.Name}' is not defined", DiagnosticKind.Name, id);
        }

        if (node is AstMemberAccess memberAccess)
        {
            var (ptr, field, _) = new MemberAccessGen(_ctx).ResolveMember(memberAccess);
            return (ptr, field.Type, field.Name, field.IsConst, field.IsWeak);
        }

        return Fail<(LLVMValueRef, BifyType, string, bool, bool)>($"Cannot assign to {node.Token.Value}", DiagnosticKind.Type, node);
    }


}