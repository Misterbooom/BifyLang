#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.CodeGen.Core.Types.Service;

public class TypeResolver
{
    public static BifyType Resolve(AstNode? typeNode, CodeGenContext ctx)
    {
        if (typeNode == null)
            return ctx.TypeRegistry.GetBifyType("void");

        if (typeNode is AstIdentifier id)
        {
            if (ctx.TypeRegistry.TryGetType(id.Name, out var type))
            {
                return type;
            }

            if (ctx.TypeRegistry.TryGetType(NameMangler.GenerateClassName(ctx.ModuleContext.Name, id.Name), out type))
            {
                return type;
            }
            return new BifyNameError("Cannot resolve type " + id.Name).Throw<BifyType>();
        }
        return typeNode switch
        {
            AstMemberAccess memberAccess => ResolveMemberType(memberAccess, ctx),
            AstUnaryOperator unary when unary.Token.Type is TokenType.POINTER or TokenType.MUL =>
                ResolvePointer(unary, ctx),
            AstIndexOperator indexOp =>
                ResolveArray(indexOp, ctx),
            _ => new BifyUnknownError($"{typeNode.GetType()} is not a known type.").Throw<BifyType>()
        };
    }

    private static BifyType ResolveMemberType(AstMemberAccess memberAccess, CodeGenContext ctx)
    {
        if (memberAccess.Left is AstIdentifier modId && memberAccess.Right is AstIdentifier typeId)
        {
            string mangled = NameMangler.GenerateClassName(modId.Name, typeId.Name);
            if (ctx.TypeRegistry.TryGetType(mangled, out var type))
                return type;
            return new BifyNameError($"Cannot resolve type '{modId.Name}.{typeId.Name}'.").Throw<BifyType>();
        }
        return new BifyUnknownError($"Invalid type expression.").Throw<BifyType>();
    }

    private static BifyType ResolvePointer(AstUnaryOperator unary, CodeGenContext ctx)
    {
        var baseType = Resolve(unary.Operand, ctx);
        string baseName = baseType is ClassType cls ? cls.FullName : baseType.Name;
        return ctx.TypeRegistry.GetBifyType($"{baseName}*");
    }

    private static BifyType ResolveArray(AstIndexOperator indexOp, CodeGenContext ctx)
    {
        var elemType = Resolve(indexOp.TargetNode, ctx);
        string elemName = elemType is ClassType cls ? cls.FullName : elemType.Name;
        string arrName = indexOp.IndexNode is AstInteger num
            ? $"{elemName}[{num.Value}]"
            : $"{elemName}[]";
        return ctx.TypeRegistry.GetBifyType(arrName);
    }
}