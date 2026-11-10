using System;
using System.Collections.Generic;
using System.Diagnostics;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Expressions;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Statements;

public class StatementDispatcher(CodeGenContext ctx)
{
    private CodeGenContext _ctx = ctx;

    public void EmitBlock(AstBlock blockNode)
    {
        foreach (var node in blockNode.ChildNodes)
        {
            if (_ctx.Builder.InsertBlock.Terminator.Handle != IntPtr.Zero)
            {
                break;
            }
            EmitStatement(node);
        }
    }

    public void EmitBlock(List<AstNode> nodeList)
    {
        foreach (var node in nodeList)
        {
            if (_ctx.Builder.InsertBlock.Terminator.Handle != IntPtr.Zero)
            {
                break;
            }
            EmitStatement(node);
            
        }
    }

    public void EmitStatement(AstNode node)
    {
        if (node is AstLine line)
        {
            node = line.Child;
        }

        Traceback.Instance.Line = node.LineNumber;

        switch (node)
        {
            case AstVarDecl varDecl:
                new VariableDeclGen(_ctx).EmitVariable(varDecl);
                break;
            case AstReturn astReturn:
                new ReturnGen(_ctx).EmitReturn(astReturn);
                break;
            case AstAssignmentOperator astAssignmentOperator:
                new AssignmentGen(_ctx).EmitAssign(astAssignmentOperator);
                break;
            case AstUnaryOperator astUnaryOperator:
                new UnaryOperatorGen(_ctx).EmitUnaryOperator(astUnaryOperator, true);
                break;
            case AstIf astIf:
                new IfGen(_ctx).EmitIfStatement(astIf);
                break;
            case AstWhile astWhile:
                new WhileGen(_ctx).EmitWhileLoop(astWhile);
                break;
            case AstFor astFor:
                new ForGen(_ctx).EmitForLoop(astFor);
                break;
            case AstCall or AstNew:
                new ExpressionDispatcher(_ctx).EmitExpression(node);
                break;
            case AstBreak astBreak:
                new JumpGen(_ctx).EmitBreak(astBreak);
                break;
            case AstContinue astContinue:
                new JumpGen(_ctx).EmitContinue(astContinue);
                break;
            case AstSwitch astSwitch:
                new SwitchGen(_ctx).EmitSwitch(astSwitch);
                break;
            default:
                new BifySyntaxError($"Invalid statement.").Throw();
                break;
        }
        _ctx.HeapControl.ReleaseTemps();
    }
}