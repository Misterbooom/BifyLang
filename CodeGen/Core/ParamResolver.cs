#nullable enable
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.CodeGen.Core;

public static class ParamResolver
{
    public static List<ParameterInfo> Resolve(AstNode? node, CodeGenContext ctx)
    {
        var parameters = new List<ParameterInfo>();
        Traverse(node, parameters, ctx);
        return parameters;
    }

    private static void Traverse(AstNode? node, List<ParameterInfo> parameters, CodeGenContext ctx)
    {
        if (node == null)
            return;

        if (node is AstParam astParam)
        {
            string name = astParam.Name.Token.Value;
            if (parameters.Any(p => p.Name == name))
                new BifyNameError($"Duplicated parameter: '{name}'").Throw();

            bool isConst = astParam.Flag is AstFlag flag && flag.HasFlag("const");
            BifyType type = TypeResolver.Resolve(astParam.Type, ctx);
            parameters.Add(new ParameterInfo(name, type, isConst));
            return;
        }

        if (node.Token.Type == TokenType.COMMA)
        {
            Traverse(node.Right, parameters, ctx);
            Traverse(node.Left, parameters, ctx);
            return;
        }

        new BifySyntaxError("Expected an AstParam node").Throw();
    }
    public static void FlattenComma(AstNode? node, List<AstNode> list)
    {
        if (node == null) return;
        if (node is AstBinaryOp bin && bin.Token.Type == TokenType.COMMA)
        {
            FlattenComma(bin.Left, list);
            FlattenComma(bin.Right, list);
        }
        else
        {
            list.Add(node);
        }
    }
}
