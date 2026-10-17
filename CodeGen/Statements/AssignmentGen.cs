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
        var (pointer, type, name, isConstant) = ResolveTarget(assignmentOp.IdentifierNode);
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

        TypedValue casted = (binOp.HasValue && type.Name == "char" && value.Type.Name == "int")
            ? TypeCaster.ExplicitCast(_ctx, value, type)
            : TypeCaster.ImplicitCast(_ctx, value, type);

        _ctx.Builder.BuildStore(casted.Value, pointer);
    }

    private (LLVMValueRef Pointer, BifyType Type, string Name, bool IsConstant) ResolveTarget(AstNode node)
    {
        if (node is AstIdentifier id)
        {
            if (_ctx.CurrentScope.TryLookup(id.Name, out var symbol))
                return (symbol.Pointer, symbol.Type, symbol.Name, symbol.IsConstant);

            if (_ctx.CurrentScope.TryLookup("this", out var thisSym) &&
                thisSym.Type is ClassType ct &&
                ct.Fields.TryGetValue(id.Name, out var f) &&
                !f.IsMethod)
            {
                MemberAccessGen.CheckAccess(_ctx, ct, f);
                var thisVal = _ctx.Builder.BuildLoad2(thisSym.Type.Type, thisSym.Pointer, "this");
                var ptr = _ctx.Builder.BuildStructGEP2(ct.StructType, thisVal, (uint)f.Index, f.Name);
                return (ptr, f.Type, f.Name, false);
            }

            return new BifyNameError($"Variable '{id.Name}' is not defined").Throw<(LLVMValueRef, BifyType, string, bool)>();
        }

        if (node is AstMemberAccess memberAccess)
        {
            var (ptr, field, _) = MemberAccessGen.ResolveMember(_ctx, memberAccess);
            return (ptr, field.Type, field.Name, false);
        }

        return new BifyTypeError($"Cannot assign to {node.Token.Value}").Throw<(LLVMValueRef, BifyType, string, bool)>();
    }
}