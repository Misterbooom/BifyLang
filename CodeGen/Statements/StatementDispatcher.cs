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
            EmitStatement(node);
        }
    }

    public void EmitBlock(List<AstNode> nodeList)
    {
        foreach (var node in nodeList)
        {
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
                new ControlFlowGen(_ctx).EmitIfStatement(astIf);
                break;
            case AstWhile astWhile:
                
                new ControlFlowGen(_ctx).EmitWhileLoop(astWhile);
                break;
            case AstFor astFor:
                new ControlFlowGen(_ctx).EmitForLoop(astFor);
                break;
            case AstCall astCall:
                var callRes = new CallGen(_ctx).EmitCall(astCall);
                if (callRes.Type is ClassType && callRes.IsOwned)
                    _ctx.HeapControl.BuildReleaseStrong(callRes.Value);
                break;
            case AstBreak astBreak:
                new ControlFlowGen(_ctx).EmitBreak(astBreak);
                break;
            case AstContinue astContinue:
                new ControlFlowGen(_ctx).EmitContinue(astContinue);
                break;
            case AstSwitch astSwitch :
                new ControlFlowGen(_ctx).EmitSwitch(astSwitch);
                break;
            default:
                new BifySyntaxError($"Invalid statement.").Throw();
                break;
        }
    }
}