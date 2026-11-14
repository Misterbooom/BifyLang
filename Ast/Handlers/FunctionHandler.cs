using System.Collections.Generic;
using BifyLang.Ast.Validators;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class FunctionHandler(AstBuilder builder) : TokenHandler(builder)
    {
        public override void HandleToken(Token token)
        {
            if (!DeclarationParser.TryParseHeader(Builder, out DeclarationHeader header) ||
                header.Kind != DeclarationKind.Function)
            {
                Builder.Fail(
                    "Invalid function declaration syntax. Expected `Type name(params) { ... }`.",
                    DiagnosticKind.Syntax,
                    token
                );
                return;
            }
            ParseDeclaration(header);
        }

        public void ParseDeclaration(DeclarationHeader header)
        {
            Builder.TokenIndex = header.EndIndex;
            ParseFunction(header.NameNode.Token, header.TypeNode, header.NameNode, header.FlagNode);
        }

        public void ParseFunction(Token token, AstNode typeNode, AstIdentifier nameNode, AstFlag flagNode)
        {
            bool isPoison = false;
            List<List<Token>> splitedTokens = TokensFormatter.SplitTokensByType(Builder.GetConditionTokens(), TokenType.COMMA);
            AstParam[] parametersNodes = BuildParameters(splitedTokens, ref isPoison);
            AstBinaryOp parameters = ConnectParameters(parametersNodes);
            AstBlock blockNode = Builder.HandleBody("Function");

            try
            {
                FunctionDeclarationValidator.Validate(Builder, nameNode.Token, parameters, blockNode, typeNode);
            }
            catch (BifyCompilationException)
            {
                isPoison = true;
            }

            AstNode functionNode = new AstFunctionDecl(token, typeNode, nameNode, parameters, blockNode, flagNode)
            {
                IsPoison = isPoison
            };
            Builder.CurrentNode = functionNode;
            Builder.TokenIndex++;
            Builder.Nodes.Clear();
        }

        private AstBinaryOp ConnectParameters(AstParam[] parametersNodes)
        {
            AstBinaryOp binaryOp = null;
            Token commaToken = new Token(TokenType.COMMA, ",");
            for (int i = 0; i < parametersNodes.Length; i++)
            {
                if (i == 0)
                {
                    binaryOp = new AstBinaryOp(commaToken, parametersNodes[i], null);
                }
                else
                {
                    binaryOp = new AstBinaryOp(commaToken, parametersNodes[i], binaryOp);
                }
            }
            return binaryOp;
        }

        private AstParam[] BuildParameters(List<List<Token>> splitedTokens, ref bool isPoison)
        {
            int paramIndex = 0;
            List<AstParam> parameters = new List<AstParam>();
            foreach (var group in splitedTokens)
            {
                int beforeCount = Builder.Diagnostics.Diagnostics.Count;
                try
                {
                    AstBuilder b = new(group, Builder.Diagnostics, Builder.FilePath, true);
                    AstVarDecl parametersNode = b.BuildNode() as AstVarDecl;

                    if (parametersNode == null)
                    {
                        if (Builder.Diagnostics.Diagnostics.Count == beforeCount)
                        {
                            Builder.Error(
                                $"Parameter #{paramIndex + 1}: Invalid parameter declaration.",
                                DiagnosticKind.Syntax,
                                group.Count > 0 ? group[0] : null
                            );
                        }
                        isPoison = true;
                        paramIndex++;
                        continue;
                    }

                    if (parametersNode.IsPoison)
                    {
                        isPoison = true;
                    }

                    AstParam param = new(parametersNode.Type, parametersNode.AssignmentNode.Left, parametersNode.Flag);
                    parameters.Add(param);
                }
                catch (BifyCompilationException)
                {
                    if (Builder.Diagnostics.Diagnostics.Count == beforeCount && !group.Exists(t => t.IsPoison))
                    {
                        Builder.Error(
                            $"Parameter #{paramIndex + 1}: Invalid parameter declaration.",
                            DiagnosticKind.Syntax,
                            group.Count > 0 ? group[0] : null
                        );
                    }
                    isPoison = true;
                }
                paramIndex++;
            }
            return parameters.ToArray();
        }
    }
}
