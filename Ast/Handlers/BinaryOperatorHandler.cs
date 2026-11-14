using System.Collections.Generic;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class BinaryOperatorHandler(AstBuilder builder) : TokenHandler(builder)
    {
        private Token _binaryOpToken = null;
        bool _isUnary = false;

        public override void HandleToken(Token token)
        {
            
            _binaryOpToken = token;

            if (Builder.Nodes.Count == 0)
            {
                _isUnary = true;
            }
            else
            {
                Token previousToken = Builder.GetPreviousToken();
                if (previousToken != null)
                {
                    _isUnary = previousToken.Type == TokenType.LPAREN || TokenConfig.BinaryOperators.ContainsValue(previousToken.Type);
                }
            }

            if (_isUnary)
            {
                Builder.TokenIndex++;
                AstNode operand = ParsePrimary();
                if (token.Type == TokenType.MUL)
                {
                    token.Type = TokenType.POINTER;
                }
                Builder.CurrentNode = new AstUnaryOperator(token, operand,true);
                return;
            }

            AstNode left = Builder.Nodes[^1];
            Builder.Nodes.RemoveAt(Builder.Nodes.Count - 1);

            int precedence = AstConfig.Precedence[token.Type];
            Builder.TokenIndex++;

            AstNode right = ParseRight(precedence);
            if (_isUnary)
            {
                Builder.Nodes.Add(left);
                Token newToken = token;
                if (token.Type == TokenType.MUL)
                {
                    newToken.Type = TokenType.POINTER;
                }
                Builder.CurrentNode = new AstUnaryOperator(newToken, right,true);
            }
            else
            {
                Builder.CurrentNode = new AstBinaryOp(token, left, right);

            }
        }

        private AstNode ParseRight(int minPrecedence)
        {
            AstNode left = ParsePrimary();
            while (!Builder.IsAtEnd())
            {
                Token next = Builder.Peek();
                
                if (_binaryOpToken.Type == TokenType.MUL && next.Type == TokenType.ASSIGN )
                {
                    _isUnary = true;
                    break;
                }

                if (!AstConfig.Precedence.TryGetValue(next.Type, out int prec) || prec < minPrecedence)
                    break;
                Token op = Builder.NextToken();
                AstNode right = ParseRight(prec + 1);
                left = new AstBinaryOp(op, left, right);
            }
            return left;
        }
        private bool CheckFunctionDeclaration()
        {
            return false;
        }


        public AstNode ParsePrimary(bool handlePostfix = true)
        {
            if (Builder.IsAtEnd())
            {
                Builder.Fail(
                    $"Unexpected end of expression. Please check that your expression is complete.: {Builder.GetPreviousToken()}",
                    DiagnosticKind.Syntax,
                    Builder.GetPreviousToken()
                );
            }

            Token token = Builder.NextToken();
            if (token.Type == TokenType.POISON)
            {
                return Builder.AbortPoison<AstNode>();
            }

            AstNode baseNode = token.Type switch
            {
                TokenType.IDENTIFIER or TokenType.CONST => new IdentifierHandler(Builder).ParseIdentifier(token, true),
                TokenType.NUMBER => NodeConventer.TokenToNode(token, Builder),
                TokenType.LPAREN => ParseParenthesizedExpression(),
                TokenType.SUB or TokenType.MUL or TokenType.INCREMENT or TokenType.DECREMENT or TokenType.NOT => HandleUnaryOperator(token),
                TokenType.LBRACKET => new ArrayHandler(Builder).GetArrayNode(token),
                TokenType.STRING or TokenType.CHAR => NodeConventer.TokenToNode(token, Builder),
                TokenType.NEW => new NewHandler(Builder).ParseNewExpression(token),
                _ => Builder.Fail<AstNode>(
                    $"Unexpected token '{token.Value}' found in expression. Verify your syntax and try again.",
                    DiagnosticKind.Syntax,
                    token
                )
            };
            if (baseNode != null)
            {
                if (baseNode.LineNumber == 0)
                {
                    baseNode.LineNumber = token.Line;
                }
                if (baseNode.ColumnNumber == 0)
                {
                    baseNode.ColumnNumber = token.Column;
                }
            }
            if (handlePostfix)
                return ParsePostfix(baseNode);
            else
                return baseNode;
        }

        private AstNode ParsePostfix(AstNode expr)
        {
            while (!Builder.IsAtEnd())
            {
                Token next = Builder.Peek();
                if (next.Type == TokenType.LPAREN)
                {
                    var args = Builder.ParseTokens(Builder.GetConditionTokens());
                    expr = new AstCall(next, expr, args);
                }
                else if (next.Type == TokenType.LBRACKET)
                {
                    var indexTokens = TokensFormatter.GetTokensBetween(
                        Builder.Tokens,
                        ref Builder.TokenIndex,
                        TokenType.LBRACKET,
                        TokenType.RBRACKET,
                        Builder.Diagnostics,
                        Builder.FilePath
                    );

                    var indexNode = Builder.ParseTokens(indexTokens);
                    expr = new AstIndexOperator(indexNode, expr);
                }
                else if (next.Type is TokenType.INCREMENT or TokenType.DECREMENT)
                {
                    expr = new AstUnaryOperator(next, expr);
                }
                else if (next.Type == TokenType.DOT)
                {
                    Builder.NextToken();
                    expr = new AstMemberAccess(next, expr, ParsePrimary(false));
                    Builder.TokenIndex--;
                }
                else
                {
                    break;
                }
                if (expr.LineNumber == 0)
                {
                    expr.LineNumber = next.Line;
                }
                if (expr.ColumnNumber == 0)
                {
                    expr.ColumnNumber = next.Column;
                }
                Builder.TokenIndex++;
            }
            return expr;
        }

        private AstNode  HandleUnaryOperator(Token opToken)
        {
            AstNode operand = ParsePrimary();
            if (opToken.Type == TokenType.MUL)
            {
                opToken.Type = TokenType.POINTER;
            }
            return new AstUnaryOperator(opToken, operand,true);
        }

        private AstNode ParseParenthesizedExpression()
        {
            Builder.TokenIndex--;
            List<Token> innerTokens = Builder.GetConditionTokens();
            AstNode node = Builder.ParseTokens(innerTokens);
            Builder.TokenIndex++;
            if (node?.Token.Type == TokenType.POINTER || node is AstIdentifier || IsQualifiedTypeNode(node))
            {
                AstNode valueNode = ParsePrimary();
                AstCast castNode = new(node.Token, node, valueNode);
                return castNode;
            }
            return node;
        }

        private static bool IsQualifiedTypeNode(AstNode node) =>
            node is AstMemberAccess { Right: AstIdentifier } ma &&
            (ma.Left is AstIdentifier || IsQualifiedTypeNode(ma.Left));
    }
}
