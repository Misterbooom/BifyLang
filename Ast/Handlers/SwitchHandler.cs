#nullable enable
using System.Collections.Generic;
using BifyLang.Ast.Validators;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers;

internal class SwitchHandler(AstBuilder builder) : TokenHandler(builder)
{
    public override void HandleToken(Token token)
    {
        List<Token> conditionTokens = Builder.GetConditionTokens();
        AstNode? expressionNode = conditionTokens.Count > 0 ? Builder.ParseTokens(conditionTokens) : null;
        if (expressionNode == null)
        {
            SwitchValidator.Validate(new AstSwitch(token, null!));
            return;
        }

        List<Token>? bodyTokens = null;
        if (Builder.GetNextToken()?.Type == TokenType.LCUR)
        {
            bodyTokens = Builder.GetBlockTokens();
        }
        if (bodyTokens == null)
        {
            new BifySyntaxError("Switch body not found.").Throw();
            return;
        }

        List<AstCase> cases = [];
        AstDefault? defaultNode = null;

        int i = 0;
        while (i < bodyTokens.Count)
        {
            Token current = bodyTokens[i];
            if (current.Type == TokenType.CASE)
            {
                Token caseToken = current;
                i++;

                List<Token> valueTokens = [];
                int parenDepth = 0;
                int bracketDepth = 0;
                while (i < bodyTokens.Count)
                {
                    Token vt = bodyTokens[i];
                    if (vt.Type == TokenType.LPAREN) parenDepth++;
                    else if (vt.Type == TokenType.RPAREN) parenDepth--;
                    else if (vt.Type == TokenType.LBRACKET) bracketDepth++;
                    else if (vt.Type == TokenType.RBRACKET) bracketDepth--;
                    else if (parenDepth == 0 && bracketDepth == 0 && vt.Type == TokenType.COLON)
                    {
                        break;
                    }
                    valueTokens.Add(vt);
                    i++;
                }

                if (i >= bodyTokens.Count || bodyTokens[i].Type != TokenType.COLON)
                {
                    SwitchValidator.ThrowExpectedColonAfterCase(caseToken);
                    return;
                }
                i++; // skip COLON

                AstNode? valueNode = valueTokens.Count > 0 ? Builder.ParseTokens(valueTokens) : null;
                if (valueNode == null)
                {
                    new BifySyntaxError("Expected expression after 'case'.").Throw();
                    return;
                }

                List<Token> caseBodyTokens = [];
                if (i < bodyTokens.Count && bodyTokens[i].Type == TokenType.LCUR)
                {
                    caseBodyTokens = TokensFormatter.GetTokensBetween(bodyTokens, ref i, TokenType.LCUR, TokenType.RCUR);
                    i++; // skip RCUR
                }
                else
                {
                    int curDepth = 0;
                    while (i < bodyTokens.Count)
                    {
                        Token bt = bodyTokens[i];
                        if (bt.Type == TokenType.LCUR) curDepth++;
                        else if (bt.Type == TokenType.RCUR) curDepth--;
                        else if (curDepth == 0 && (bt.Type == TokenType.CASE || bt.Type == TokenType.DEFAULT))
                        {
                            break;
                        }
                        caseBodyTokens.Add(bt);
                        i++;
                    }
                }

                AstBlock caseBlock = Builder.ParseBlock(caseBodyTokens);
                cases.Add(new AstCase(caseToken, valueNode, caseBlock));
            }
            else if (current.Type == TokenType.DEFAULT)
            {
                Token defaultToken = current;
                i++;

                if (i >= bodyTokens.Count || bodyTokens[i].Type != TokenType.COLON)
                {
                    SwitchValidator.ThrowExpectedColonAfterDefault(defaultToken);
                    return;
                }
                i++; // skip COLON

                if (defaultNode != null)
                {
                    SwitchValidator.ThrowMultipleDefaultError(defaultToken);
                    return;
                }

                List<Token> defaultBodyTokens = [];
                if (i < bodyTokens.Count && bodyTokens[i].Type == TokenType.LCUR)
                {
                    defaultBodyTokens = TokensFormatter.GetTokensBetween(bodyTokens, ref i, TokenType.LCUR, TokenType.RCUR);
                    i++; // skip RCUR
                }
                else
                {
                    int curDepth = 0;
                    while (i < bodyTokens.Count)
                    {
                        Token bt = bodyTokens[i];
                        if (bt.Type == TokenType.LCUR) curDepth++;
                        else if (bt.Type == TokenType.RCUR) curDepth--;
                        else if (curDepth == 0 && (bt.Type == TokenType.CASE || bt.Type == TokenType.DEFAULT))
                        {
                            break;
                        }
                        defaultBodyTokens.Add(bt);
                        i++;
                    }
                }

                AstBlock defaultBlock = Builder.ParseBlock(defaultBodyTokens);
                defaultNode = new AstDefault(defaultToken, defaultBlock);
            }
            else if (current.Type is TokenType.EOL or TokenType.SEMICOLON)
            {
                i++;
            }
            else
            {
                SwitchValidator.ThrowInvalidSwitchBodyToken(current);
                return;
            }
        }

        AstSwitch astSwitch = new AstSwitch(token, expressionNode, cases, defaultNode);
        SwitchValidator.Validate(astSwitch);

        Builder.CurrentNode = astSwitch;
        Builder.TokenIndex++;
    }
}
