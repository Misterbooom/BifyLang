#nullable enable
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public static class ClassMemberCollector
{
    public static ClassField CollectField(AstVarDecl varDecl, CodeGenContext ctx, ClassType classType, int index)
    {
        string name = varDecl.AssignmentNode.Left.Token.Value;
        var flags = varDecl.Flag?.Flags.Select(f => f.Token.Value) ?? [];

        if (!FlagChecker.CheckAttributeFlags(flags, out var error))
        {
            new BifyTypeError(error).Throw();
        }

        var accessLevel = ParseAccessLevel(varDecl.Flag);
        var type = TypeResolver.Resolve(varDecl.Type, ctx);
        var value = varDecl.AssignmentNode.Right;
        bool isConst = varDecl.Flag?.HasFlag("const") ?? false;
        bool isWeak = varDecl.Flag?.HasFlag("weak") ?? false;

        return new ClassField(name, type, value, accessLevel, index, isConst, isWeak, classType);
    }

    public static ClassMethod CollectMethod(AstFunctionDecl funcDecl, CodeGenContext ctx, ClassType classType)
    {
        string name = funcDecl.FunctionNameNode.Name;
        var flags = funcDecl.FlagNode?.Flags.Select(f => f.Token.Value) ?? [];

        if (!FlagChecker.CheckMethodFlags(flags, out var error))
        {
            new BifyTypeError(error).Throw();
        }

        var accessLevel = ParseAccessLevel(funcDecl.FlagNode);
        bool isVirtual = funcDecl.FlagNode?.HasFlag("virtual") ?? false;
        bool isOverride = funcDecl.FlagNode?.HasFlag("override") ?? false;
        var parameters = ParamResolver.Resolve(funcDecl.ArgumentsNode, ctx);

        BifyType returnType;
        if (name == "constructor")
        {
            if (isVirtual || isOverride)
                new BifyTypeError("Constructor cannot be marked 'virtual' or 'override'.").Throw();

            returnType = BifyType.CreateVoid(ctx);
        }
        else if (name == "destructor")
        {
            if (parameters.Count > 0)
                new BifyTypeError("Destructor cannot have parameters.").Throw();

            isVirtual = true;
            returnType = BifyType.CreateVoid(ctx);
        }
        else
        {
            returnType = TypeResolver.Resolve(funcDecl.TypeNode, ctx);
        }

        var paramTypes = new List<LLVMTypeRef> { LLVMTypeRef.CreatePointer(ctx.Context.Int8Type, 0) };
        paramTypes.AddRange(parameters.Select(p => p.Type.Type));
        var mangledName = NameMangler.GenerateMethodName(ctx.ModuleContext.Name, classType.Name, name);
        var funcType = LLVMTypeRef.CreateFunction(returnType.Type, paramTypes.ToArray(), false);
        var signature = new CallableType(name, mangledName, [..parameters], returnType, funcType, true);
        var functionValue = ctx.Module.AddFunction(mangledName, funcType);
        
        return new ClassMethod(name, signature, functionValue, accessLevel, -1, classType, funcDecl.BlockNode, isVirtual, isOverride, parameters);
    }

    public static AccessLevel ParseAccessLevel(AstFlag? flag)
    {
        if (flag == null)
            return AccessLevel.PUBLIC;
        if (flag.HasFlag("private"))
            return AccessLevel.PRIVATE;
        if (flag.HasFlag("protected"))
            return AccessLevel.PROTECTED;
        return AccessLevel.PUBLIC;
    }
}
