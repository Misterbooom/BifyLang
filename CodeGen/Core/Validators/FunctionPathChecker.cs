#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Validators;

public class FunctionPathChecker
{
    public readonly bool AllPathsReturn = false;
    private readonly BifyType _functionType;

    public FunctionPathChecker(BifyType functionType, AstNode? node)
    {
        _functionType = functionType;
        if (node == null) return;

        AllPathsReturn = Check(node);
        if (_functionType.Type.Kind != LLVMTypeKind.LLVMVoidTypeKind && !AllPathsReturn)
        {
            Traceback.Instance.ThrowException(new BifyTypeError($"Not all execution paths return a value for {_functionType.Name}"));
        }
    }

    public FunctionPathChecker(AstFunctionDecl functionDecl, CodeGenContext ctx)
        : this(TypeResolver.Resolve(functionDecl.TypeNode, ctx), functionDecl.BlockNode)
    {
    }

    public bool Check(AstNode? node)
    {
        if (node == null) return false;

        if (node.Token != null && node.Token.Line > 0)
            Traceback.Instance.SetCurrentLine(node.Token.Line);

        return node switch
        {
            AstReturn astReturn => CheckReturn(astReturn),
            AstBlock astBlock => CheckBlock(astBlock),
            AstIf astIf => CheckIf(astIf),
            AstWhile astWhile => CheckWhile(astWhile),
            AstFor astFor => CheckFor(astFor),
            AstSwitch astSwitch => CheckSwitch(astSwitch),
            AstLine astLine => Check(astLine.Child),
            _ => false
        };
    }

    private bool CheckReturn(AstReturn astReturn)
    {
        if (astReturn.ArgumentsNode == null && _functionType.Type.Kind != LLVMTypeKind.LLVMVoidTypeKind)
        {
            Traceback.Instance.ThrowException(new BifyTypeError($"Expected '{_functionType.Name}' but got 'void'"));
        }
        return true;
    }

    private bool CheckBlock(AstBlock astBlock)
    {
        foreach (var child in astBlock.ChildNodes)
        {
            var stmt = child is AstLine line ? line.Child : child;
            if (Check(stmt))
                return true;
        }
        return false;
    }

    private bool CheckIf(AstIf astIf)
    {
        if (astIf.ElseNode?.BlockNode == null)
            return false;

        if (!Check(astIf.BlockNode))
            return false;

        foreach (var elseIf in astIf.ElseIfNodes)
        {
            if (!Check(elseIf.BlockNode))
                return false;
        }

        return Check(astIf.ElseNode.BlockNode);
    }

    private bool CheckWhile(AstWhile astWhile)
    {
        if (IsInfiniteCondition(astWhile.ConditionNode, allowNull: false) && !HasBreak(astWhile.BlockNode))
            return Check(astWhile.BlockNode);

        return false;
    }

    private bool CheckFor(AstFor astFor)
    {
        if (IsInfiniteCondition(astFor.ConditionNode, allowNull: true) && !HasBreak(astFor.BlockNode))
            return Check(astFor.BlockNode);

        return false;
    }

    private static bool IsInfiniteCondition(AstNode? conditionNode, bool allowNull)
    {
        if (conditionNode == null)
            return allowNull;

        return conditionNode is AstIdentifier { Name: "true" or "1" } ||
               conditionNode is AstBoolean { Value: true } ||
               conditionNode is AstInteger { Value: not 0 };
    }

    private bool CheckSwitch(AstSwitch astSwitch)
    {
        if (astSwitch.DefaultNode == null)
            return false;

        if (!Check(astSwitch.DefaultNode.BlockNode))
            return false;

        foreach (var c in astSwitch.Cases)
        {
            if (!Check(c.BlockNode))
                return false;
        }

        return true;
    }

    private static bool HasBreak(AstNode? node)
    {
        if (node == null) return false;
        if (node is AstBreak) return true;

        if (node is AstBlock block)
        {
            foreach (var child in block.ChildNodes)
            {
                var stmt = child is AstLine line ? line.Child : child;
                if (HasBreak(stmt)) return true;
            }
        }
        else if (node is AstIf ifNode)
        {
            if (HasBreak(ifNode.BlockNode)) return true;
            foreach (var elseIf in ifNode.ElseIfNodes)
                if (HasBreak(elseIf.BlockNode)) return true;
            if (ifNode.ElseNode != null && HasBreak(ifNode.ElseNode.BlockNode)) return true;
        }

        if (node is AstWhile or AstFor or AstSwitch) return false;

        return false;
    }
}