#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class UnaryOpGen(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public TypedValue EmitUnaryOperator(AstUnaryOperator op, bool isStatement = false)
    {
        if (op.Token.Type is TokenType.INCREMENT or TokenType.DECREMENT)
        {
            return HandleIncDec(op);
        }

        if (isStatement)
        {
            return new BifySyntaxError("Invalid statement!").Throw<TypedValue>();
        }

        var value = new ExpressionDispatcher(_ctx).EmitExpression(op.Operand);
        return op.Token.Type switch
        {
            TokenType.SUB => HandleNeg(value),
            TokenType.ADD => HandlePos(value),
            TokenType.NOT => HandleNot(value),
            _ => new BifySyntaxError($"Unexpected unary operator: {op.Token.Value}").Throw<TypedValue>()
        };
    }

    private TypedValue HandleIncDec(AstUnaryOperator op)
    {
        LLVMValueRef pointer;
        BifyType type;
        string name;
        bool isConst = false;

        if (op.Operand is AstIdentifier id)
        {
            if (_ctx.CurrentScope.TryLookup(id.Name, out var symbol))
            {
                pointer = symbol.Pointer;
                type = symbol.Type;
                name = symbol.Name;
                isConst = symbol.IsConstant;
            }
            else
            {
                return new BifyNameError($"Variable '{id.Name}' is not defined").Throw<TypedValue>();
            }
        }
        else
        {
            return new BifyTypeError($"Operator '{op.Token.Value}' requires a variable").Throw<TypedValue>();
        }

        if (isConst)
        {
            return new BifyTypeError($"Cannot mutate constant variable '{name}'").Throw<TypedValue>();
        }

        var curr = _ctx.Builder.BuildLoad2(type.Type, pointer, name);
        var isInc = op.Token.Type == TokenType.INCREMENT;

        LLVMValueRef next;
        if (type.Name is "int" or "char")
        {
            var step = LLVMValueRef.CreateConstInt(type.Type, 1, true);
            next = isInc ? _ctx.Builder.BuildAdd(curr, step, "inc") : _ctx.Builder.BuildSub(curr, step, "dec");
        }
        else if (type.Name is "float" or "double")
        {
            var step = LLVMValueRef.CreateConstReal(type.Type, 1.0);
            next = isInc ? _ctx.Builder.BuildFAdd(curr, step, "finc") : _ctx.Builder.BuildFSub(curr, step, "fdec");
        }
        else
        {
            return new BifyTypeError($"Operator '{op.Token.Value}' is not supported for type '{type.Name}'").Throw<TypedValue>();
        }

        _ctx.Builder.BuildStore(next, pointer);
        return new TypedValue(op.IsPrefix ? next : curr, type);
    }

    private TypedValue HandleNeg(TypedValue val)
    {
        if (val.Type.Name is "int" or "char")
        {
            return new TypedValue(_ctx.Builder.BuildNeg(val.Value, "negtmp"), val.Type);
        }

        if (val.Type.Name is "float" or "double")
        {
            return new TypedValue(_ctx.Builder.BuildFNeg(val.Value, "fnegtmp"), val.Type);
        }

        return new BifyTypeError($"Cannot negate type '{val.Type.Name}'").Throw<TypedValue>();
    }

    private static TypedValue HandlePos(TypedValue val)
    {
        if (val.Type.Name is "int" or "char" or "float" or "double")
        {
            return val;
        }

        return new BifyTypeError($"Cannot apply '+' to type '{val.Type.Name}'").Throw<TypedValue>();
    }

    private TypedValue HandleNot(TypedValue val)
    {
        var boolType = BifyType.CreateBool(_ctx);
        if (val.Type.Name == "bool")
        {
            return new TypedValue(_ctx.Builder.BuildNot(val.Value, "nottmp"), boolType);
        }

        if (val.Type.Name is "int" or "char")
        {
            var zero = LLVMValueRef.CreateConstInt(val.Type.Type, 0, false);
            return new TypedValue(_ctx.Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, val.Value, zero, "nottmp"), boolType);
        }

        return new BifyTypeError($"Cannot apply '!' to type '{val.Type.Name}'").Throw<TypedValue>();
    }
}