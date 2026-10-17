#nullable enable
using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Expressions;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.Tests;

internal static class ExpressionCodeGenTests
{
    public static Dictionary<string, Func<bool>> GetTests() => new()
    {
        { "ExprCodeGen_IntegerLiteral", TestEmitIntegerLiteral },
        { "ExprCodeGen_FloatLiteral", TestEmitFloatLiteral },
        { "ExprCodeGen_BoolLiteral", TestEmitBoolLiteral },
        { "ExprCodeGen_NullLiteral", TestEmitNullLiteral },
        { "ExprCodeGen_StringLiteral", TestEmitStringLiteral },
        { "ExprCodeGen_IdentifierLookup", TestEmitIdentifier },
        { "ExprCodeGen_UndefinedIdentifierThrows", TestUndefinedIdentifierThrows },
        { "ExprCodeGen_BinaryArithmetic", TestEmitBinaryArithmetic },
        { "ExprCodeGen_BinaryComparison", TestEmitBinaryComparison },
        { "ExprCodeGen_InvalidExpressionThrows", TestInvalidExpressionThrows },
        { "ExprCodeGen_ExplicitCastFloatToInt", TestExplicitCastFloatToInt },
        { "ExprCodeGen_ExplicitCastIntToFloat", TestExplicitCastIntToFloat },
        { "ExprCodeGen_ExplicitCastFloatToDouble", TestExplicitCastFloatToDouble },
        { "ExprCodeGen_ExplicitCastFloatToBool", TestExplicitCastFloatToBool },
        { "ExprCodeGen_ExplicitCastIntToChar", TestExplicitCastIntToChar },
        { "ExprCodeGen_ExplicitCastIncompatibleThrows", TestExplicitCastIncompatibleThrows },
    };

    static bool TestEmitIntegerLiteral()
    {
        using var ctx = new CodeGenContext();
        var dispatcher = new ExpressionDispatcher(ctx);
        var token = new Token(TokenType.NUMBER, "42");
        var node = new AstInteger(token, 42);

        TypedValue result = dispatcher.EmitExpression(node);

        return result.Type.Name == "int" &&
               result.Type.Type.Kind == LLVMTypeKind.LLVMIntegerTypeKind &&
               result.Type.Type.IntWidth == 32 &&
               result.Value.ConstIntSExt == 42;
    }

    static bool TestEmitFloatLiteral()
    {
        using var ctx = new CodeGenContext();
        var dispatcher = new ExpressionDispatcher(ctx);
        var token = new Token(TokenType.NUMBER, "3.14");
        var node = new AstFloat(token, 3.14f);

        TypedValue result = dispatcher.EmitExpression(node);

        return result.Type.Name == "float" &&
               result.Type.Type.Kind == LLVMTypeKind.LLVMFloatTypeKind;
    }

    static bool TestEmitBoolLiteral()
    {
        using var ctx = new CodeGenContext();
        var dispatcher = new ExpressionDispatcher(ctx);

        var trueNode = new AstBoolean(new Token(TokenType.TRUE, "true"), true);
        TypedValue trueResult = dispatcher.EmitExpression(trueNode);

        var falseNode = new AstBoolean(new Token(TokenType.FALSE, "false"), false);
        TypedValue falseResult = dispatcher.EmitExpression(falseNode);

        return trueResult.Type.Name == "bool" &&
               trueResult.Type.Type.Kind == LLVMTypeKind.LLVMIntegerTypeKind &&
               trueResult.Type.Type.IntWidth == 1 &&
               trueResult.Value.ConstIntZExt == 1 &&
               falseResult.Type.Name == "bool" &&
               falseResult.Value.ConstIntZExt == 0;
    }

    static bool TestEmitNullLiteral()
    {
        using var ctx = new CodeGenContext();
        var dispatcher = new ExpressionDispatcher(ctx);
        var token = new Token(TokenType.NULL, "null");
        var node = new AstNull(token);

        TypedValue result = dispatcher.EmitExpression(node);

        return result.Type.Name == "null" &&
               result.Type.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind;
    }

    static bool TestEmitStringLiteral()
    {
        using var ctx = new CodeGenContext();
        var dispatcher = new ExpressionDispatcher(ctx);
        var token = new Token(TokenType.STRING, "hello world");
        var node = new AstString(token, "hello world");

        TypedValue result = dispatcher.EmitExpression(node);

        return result.Type.Name == "string" &&
               result.Type.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind &&
               result.Value.Handle != IntPtr.Zero;
    }

    static bool TestEmitIdentifier()
    {
        using var ctx = new CodeGenContext();
        var funcType = LLVMTypeRef.CreateFunction(ctx.Context.VoidType, [], false);
        var func = ctx.Module.AddFunction("testFunc", funcType);
        var entry = func.AppendBasicBlock("entry");
        ctx.Builder.PositionAtEnd(entry);

        var intType = BifyType.CreateInt(ctx);
        var alloca = ctx.Builder.BuildAlloca(intType.Type, "x");
        ctx.CurrentScope.Define("x", alloca, intType);

        var dispatcher = new ExpressionDispatcher(ctx);
        var idNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "x"), "x");
        TypedValue result = dispatcher.EmitExpression(idNode);

        return result.Type.Name == "int" &&
               result.Type.Type.Kind == LLVMTypeKind.LLVMIntegerTypeKind &&
               result.Value.Handle != IntPtr.Zero;
    }

    static bool TestUndefinedIdentifierThrows()
    {
        using var ctx = new CodeGenContext();
        var dispatcher = new ExpressionDispatcher(ctx);
        var idNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "undefinedVar"), "undefinedVar");

        Traceback.Instance.Catch(typeof(BifyNameError));
        try
        {
            _ = dispatcher.EmitExpression(idNode);
            var err = Traceback.Instance.GetError();
            return err is BifyNameError;
        }
        catch (ApplicationException)
        {
            return true;
        }
        catch (Exception)
        {
            return true;
        }
        finally
        {
            Traceback.Instance.TrackPop();
        }
    }

    static bool TestEmitBinaryArithmetic()
    {
        using var ctx = new CodeGenContext();
        var funcType = LLVMTypeRef.CreateFunction(ctx.Context.VoidType, [], false);
        var func = ctx.Module.AddFunction("testArith", funcType);
        var entry = func.AppendBasicBlock("entry");
        ctx.Builder.PositionAtEnd(entry);

        var dispatcher = new ExpressionDispatcher(ctx);

        // int + int: 10 + 20
        var left = new AstInteger(new Token(TokenType.NUMBER, "10"), 10);
        var right = new AstInteger(new Token(TokenType.NUMBER, "20"), 20);
        var addNode = new AstBinaryOp(new Token(TokenType.ADD, "+"), left, right);
        TypedValue addResult = dispatcher.EmitExpression(addNode);

        // float * float: 2.5 * 4.0
        var fLeft = new AstFloat(new Token(TokenType.NUMBER, "2.5"), 2.5f);
        var fRight = new AstFloat(new Token(TokenType.NUMBER, "4.0"), 4.0f);
        var mulNode = new AstBinaryOp(new Token(TokenType.MUL, "*"), fLeft, fRight);
        TypedValue mulResult = dispatcher.EmitExpression(mulNode);

        return addResult.Type.Name == "int" &&
               addResult.Value.Handle != IntPtr.Zero &&
               mulResult.Type.Name == "float" &&
               mulResult.Value.Handle != IntPtr.Zero;
    }

    static bool TestEmitBinaryComparison()
    {
        using var ctx = new CodeGenContext();
        var funcType = LLVMTypeRef.CreateFunction(ctx.Context.VoidType, [], false);
        var func = ctx.Module.AddFunction("testCmp", funcType);
        var entry = func.AppendBasicBlock("entry");
        ctx.Builder.PositionAtEnd(entry);

        var dispatcher = new ExpressionDispatcher(ctx);

        // 10 < 20 -> bool
        var left = new AstInteger(new Token(TokenType.NUMBER, "10"), 10);
        var right = new AstInteger(new Token(TokenType.NUMBER, "20"), 20);
        var ltNode = new AstBinaryOp(new Token(TokenType.LT, "<"), left, right);
        TypedValue ltResult = dispatcher.EmitExpression(ltNode);

        // true && false -> bool
        var bLeft = new AstBoolean(new Token(TokenType.TRUE, "true"), true);
        var bRight = new AstBoolean(new Token(TokenType.FALSE, "false"), false);
        var andNode = new AstBinaryOp(new Token(TokenType.AND, "&&"), bLeft, bRight);
        TypedValue andResult = dispatcher.EmitExpression(andNode);

        return ltResult.Type.Name == "bool" &&
               ltResult.Type.Type.Kind == LLVMTypeKind.LLVMIntegerTypeKind &&
               andResult.Type.Name == "bool" &&
               andResult.Value.Handle != IntPtr.Zero;
    }

    static bool TestInvalidExpressionThrows()
    {
        using var ctx = new CodeGenContext();
        var dispatcher = new ExpressionDispatcher(ctx);
        var invalidNode = new AstBlock([]);

        Traceback.Instance.Catch(typeof(BifySyntaxError));
        try
        {
            _ = dispatcher.EmitExpression(invalidNode);
            var err = Traceback.Instance.GetError();
            return err is BifySyntaxError;
        }
        catch (ApplicationException)
        {
            return true;
        }
        catch (Exception)
        {
            return true;
        }
        finally
        {
            Traceback.Instance.TrackPop();
        }
    }

    static bool TestExplicitCastFloatToInt()
    {
        using var ctx = new CodeGenContext();
        var funcType = LLVMTypeRef.CreateFunction(ctx.Context.VoidType, [], false);
        var func = ctx.Module.AddFunction("testCastFtoI", funcType);
        var entry = func.AppendBasicBlock("entry");
        ctx.Builder.PositionAtEnd(entry);

        var dispatcher = new ExpressionDispatcher(ctx);
        var typeNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "int"), "int");
        var valNode = new AstFloat(new Token(TokenType.NUMBER, "3.14"), 3.14f);
        var castNode = new AstCast(new Token(TokenType.IDENTIFIER, "int"), typeNode, valNode);

        TypedValue result = dispatcher.EmitExpression(castNode);

        return result.Type.Name == "int" &&
               result.Type.Type.Kind == LLVMTypeKind.LLVMIntegerTypeKind &&
               result.Type.Type.IntWidth == 32 &&
               result.Value.Handle != IntPtr.Zero;
    }

    static bool TestExplicitCastIntToFloat()
    {
        using var ctx = new CodeGenContext();
        var funcType = LLVMTypeRef.CreateFunction(ctx.Context.VoidType, [], false);
        var func = ctx.Module.AddFunction("testCastItoF", funcType);
        var entry = func.AppendBasicBlock("entry");
        ctx.Builder.PositionAtEnd(entry);

        var dispatcher = new ExpressionDispatcher(ctx);
        var typeNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "float"), "float");
        var valNode = new AstInteger(new Token(TokenType.NUMBER, "42"), 42);
        var castNode = new AstCast(new Token(TokenType.IDENTIFIER, "float"), typeNode, valNode);

        TypedValue result = dispatcher.EmitExpression(castNode);

        return result.Type.Name == "float" &&
               result.Type.Type.Kind == LLVMTypeKind.LLVMFloatTypeKind &&
               result.Value.Handle != IntPtr.Zero;
    }

    static bool TestExplicitCastFloatToDouble()
    {
        using var ctx = new CodeGenContext();
        var funcType = LLVMTypeRef.CreateFunction(ctx.Context.VoidType, [], false);
        var func = ctx.Module.AddFunction("testCastFtoD", funcType);
        var entry = func.AppendBasicBlock("entry");
        ctx.Builder.PositionAtEnd(entry);

        var dispatcher = new ExpressionDispatcher(ctx);
        var typeNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "double"), "double");
        var valNode = new AstFloat(new Token(TokenType.NUMBER, "3.14"), 3.14f);
        var castNode = new AstCast(new Token(TokenType.IDENTIFIER, "double"), typeNode, valNode);

        TypedValue result = dispatcher.EmitExpression(castNode);

        return result.Type.Name == "double" &&
               result.Type.Type.Kind == LLVMTypeKind.LLVMDoubleTypeKind &&
               result.Value.Handle != IntPtr.Zero;
    }

    static bool TestExplicitCastFloatToBool()
    {
        using var ctx = new CodeGenContext();
        var funcType = LLVMTypeRef.CreateFunction(ctx.Context.VoidType, [], false);
        var func = ctx.Module.AddFunction("testCastFtoB", funcType);
        var entry = func.AppendBasicBlock("entry");
        ctx.Builder.PositionAtEnd(entry);

        var dispatcher = new ExpressionDispatcher(ctx);
        var typeNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "bool"), "bool");
        var valNode = new AstFloat(new Token(TokenType.NUMBER, "1.5"), 1.5f);
        var castNode = new AstCast(new Token(TokenType.IDENTIFIER, "bool"), typeNode, valNode);

        TypedValue result = dispatcher.EmitExpression(castNode);

        return result.Type.Name == "bool" &&
               result.Type.Type.Kind == LLVMTypeKind.LLVMIntegerTypeKind &&
               result.Type.Type.IntWidth == 1 &&
               result.Value.Handle != IntPtr.Zero;
    }

    static bool TestExplicitCastIntToChar()
    {
        using var ctx = new CodeGenContext();
        var funcType = LLVMTypeRef.CreateFunction(ctx.Context.VoidType, [], false);
        var func = ctx.Module.AddFunction("testCastItoC", funcType);
        var entry = func.AppendBasicBlock("entry");
        ctx.Builder.PositionAtEnd(entry);

        var dispatcher = new ExpressionDispatcher(ctx);
        var typeNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "char"), "char");
        var valNode = new AstInteger(new Token(TokenType.NUMBER, "65"), 65);
        var castNode = new AstCast(new Token(TokenType.IDENTIFIER, "char"), typeNode, valNode);

        TypedValue result = dispatcher.EmitExpression(castNode);

        return result.Type.Name == "char" &&
               result.Type.Type.Kind == LLVMTypeKind.LLVMIntegerTypeKind &&
               result.Type.Type.IntWidth == 8 &&
               result.Value.Handle != IntPtr.Zero;
    }

    static bool TestExplicitCastIncompatibleThrows()
    {
        using var ctx = new CodeGenContext();
        var funcType = LLVMTypeRef.CreateFunction(ctx.Context.VoidType, [], false);
        var func = ctx.Module.AddFunction("testIncompatCast", funcType);
        var entry = func.AppendBasicBlock("entry");
        ctx.Builder.PositionAtEnd(entry);

        var dispatcher = new ExpressionDispatcher(ctx);
        var typeNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "float"), "float");
        var valNode = new AstString(new Token(TokenType.STRING, "hello"), "hello");
        var castNode = new AstCast(new Token(TokenType.IDENTIFIER, "float"), typeNode, valNode);

        Traceback.Instance.Clear();
        Traceback.Instance.Catch(typeof(BifyCastError));
        try
        {
            _ = dispatcher.EmitExpression(castNode);
            var err = Traceback.Instance.GetError();
            return err is BifyCastError;
        }
        catch (ApplicationException)
        {
            return true;
        }
        catch (Exception)
        {
            return true;
        }
        finally
        {
            Traceback.Instance.TrackPop();
        }
    }
}
