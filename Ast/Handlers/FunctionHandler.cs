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
                new BifySyntaxError("Invalid function declaration syntax. Expected `Type name(params) { ... }`.").Throw();
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
            List<List<Token>> splitedTokens = TokensFormatter.SplitTokensByType(Builder.GetConditionTokens(), TokenType.COMMA);
            AstParam[] parametersNodes = BuildParameters(splitedTokens);
            AstBinaryOp parameters = ConnectParameters(parametersNodes);
            AstBlock blockNode = Builder.HandleBody("Function");

            FunctionDeclarationValidator.Validate(nameNode.Token, parameters, blockNode, typeNode);

            AstNode functionNode = new AstFunctionDecl(token, typeNode, nameNode, parameters, blockNode, flagNode);
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
        private AstParam[] BuildParameters(List<List<Token>> splitedTokens)
        {
            int paramIndex = 0;
            List<AstParam> parameters = new List<AstParam>();
            foreach (var group in splitedTokens)
            {
                AstBuilder b = new AstBuilder(group);
                AstVarDecl parametersNode = b.BuildNode() as AstVarDecl;

                if (parametersNode == null)
                {
                    new BifySyntaxError($"Parameter #{paramIndex + 1}: Invalid parameter declaration.").Throw();
                }

                AstParam param = new AstParam(parametersNode.Type, parametersNode.AssignmentNode.Left, parametersNode.Flag);
                parameters.Add(param);
                paramIndex++;
            }
            return parameters.ToArray();
        }
    }
}
