#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using BifyLang.CodeGen.Statements;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Declarations.ClassHandler;

public enum AccessLevel
{
    PUBLIC,
    PRIVATE,
    PROTECTED,
}

public struct ClassField(
    string name,
    BifyType type,
    AstNode? value,
    AccessLevel accessLevel,
    int index = -1,
    int vtableIndex = -1,
    string declaringClass = "",
    bool isConst = false,
    bool isWeak = false)
{
    public readonly BifyType Type = type;
    public LLVMValueRef Value = default;
    public readonly string Name = name;
    public readonly AccessLevel AccessLevel = accessLevel;
    public readonly int Index = index;
    public readonly int VTableIndex = vtableIndex;
    public readonly string DeclaringClass = declaringClass;
    public AstNode? ValueNode = value;
    public bool IsMethod => Index < 0;
    public bool IsConst = isConst;
    public bool IsWeak = isWeak;
}

public class ClassFieldParser
{
    public static Dictionary<string, ClassField> ParseAttributes(AstClass astClass, CodeGenContext ctx, int startIndex,
        string className)
    {
        Dictionary<string, ClassField> fields = [];
        AstBlock block = (AstBlock)astClass.BodyNode;
        int index = startIndex;
        foreach (var blockChildNode in block.ChildNodes)
        {
            if (blockChildNode is AstVarDecl varDecl)
            {
                var field = ParseAttribute(varDecl, ctx, index++, className);
                if (fields.ContainsKey(field.Name))
                {
                    new BifyNameError($"Duplicate member '{field.Name}' in class").Throw();
                }

                fields.Add(field.Name, field);
            }
        }

        return fields;
    }

    public static Dictionary<string, ClassField> ParseMethods(AstClass astClass, ClassType classType,
        CodeGenContext ctx)
    {
        Dictionary<string, ClassField> fields = [];
        AstBlock block = (AstBlock)astClass.BodyNode;
        var methodDecls = block.ChildNodes.OfType<AstFunctionDecl>().ToList();
        var methodInfos =
            new List<(AstFunctionDecl Decl, LLVMValueRef FuncVal, List<ParameterInfo> Params, BifyType RetType)>();

        foreach (var method in methodDecls)
        {
            string functionName = method.FunctionNameNode.Name;
            BifyType returnType = TypeResolver.Resolve(method.TypeNode, ctx);

            var pathChecker = new FunctionPathChecker(method, ctx);
            if (!pathChecker.AllPathsReturn && returnType.Type.Kind != LLVMTypeKind.LLVMVoidTypeKind)
            {
                new BifyTypeError($"Not all execution paths return a value in function {functionName}").Throw();
            }

            List<ParameterInfo> parameters = [new ParameterInfo("this", classType, true)];
            ResolveParameters(method.ArgumentsNode, ref parameters, ctx);

            var functionType = LLVMTypeRef.CreateFunction(
                returnType.Type,
                parameters.Select(parameter => parameter.Type.Type).ToArray(),
                false
            );

            string mangledName = $"{astClass.NameNode.Token.Value}.{functionName}";
            var functionValue = ctx.Module.AddFunction(mangledName, functionType);

            int vtableIndex = -1;
            if (functionName != "constructor")
            {
                int existingIdx = classType.VTableMethodNames.IndexOf(functionName);
                if (existingIdx >= 0)
                {
                    vtableIndex = existingIdx;
                }
                else
                {
                    vtableIndex = classType.VTableMethodNames.Count;
                    classType.VTableMethodNames.Add(functionName);
                }
            }


            var field = new ClassField(
                functionName,
                new BifyType(returnType.Name, functionType),
                null,
                ParseAccessLevel(method.FlagNode, functionName == "constructor"),
                -1,
                vtableIndex,
                astClass.NameNode.Token.Value
            )
            {
                Value = functionValue
            };

            if (fields.ContainsKey(field.Name))
            {
                new BifyNameError($"Duplicate member '{field.Name}' in class").Throw();
            }

            fields[field.Name] = field;
            classType.Fields[field.Name] = field;
            methodInfos.Add((method, functionValue, parameters, returnType));
        }

        var prevBlock = ctx.Builder.InsertBlock;
        var prevEntry = ctx.FunctionEntry;
        var prevRetType = ctx.FunctionReturnType;

        foreach (var (decl, funcVal, parameters, retType) in methodInfos)
        {
            var entry = funcVal.AppendBasicBlock("entry");
            ctx.Builder.PositionAtEnd(entry);
            ctx.FunctionEntry = entry;
            ctx.FunctionReturnType = retType;

            using (ctx.EnterScope())
            {
                AddFunctionParametersToScope(funcVal, parameters, ctx);
                new StatementDispatcher(ctx).EmitBlock(decl.BlockNode);
            }

            if (retType.Type.Kind == LLVMTypeKind.LLVMVoidTypeKind &&
                ctx.Builder.InsertBlock.Terminator.Handle == IntPtr.Zero)
            {
                ctx.Builder.BuildRetVoid();
            }
        }

        if (prevBlock.Handle != IntPtr.Zero)
            ctx.Builder.PositionAtEnd(prevBlock);
        ctx.FunctionEntry = prevEntry;
        ctx.FunctionReturnType = prevRetType;

        return fields;
    }

    private static ClassField ParseAttribute(AstVarDecl varDecl, CodeGenContext ctx, int index, string className)
    {
        string name = varDecl.AssignmentNode.Left.Token.Value;
        var value = varDecl.AssignmentNode.Right;
        var accessLevel = ParseAccessLevel(varDecl.Flag);
        var type = TypeResolver.Resolve(varDecl.Type, ctx);
        if (!FlagChecker.CheckAttributeFlags(varDecl.Flag.Flags.Select(i => i.Token.Value), out var error))
        {
            return new BifyTypeError(error).Throw<ClassField>();
        }

        return new ClassField(name, type, value, accessLevel, index, -1, className, varDecl.Flag.HasFlag("const"),
            varDecl.Flag.HasFlag("weak"));
    }

    private static AccessLevel ParseAccessLevel(AstFlag astFlag, bool isConstructor = false)
    {
        AccessLevel accessLevel = isConstructor ? AccessLevel.PUBLIC : AccessLevel.PRIVATE;
        foreach (var flag in astFlag.Flags)
        {
            switch (flag.Token.Value)
            {
                case "public":
                    accessLevel = AccessLevel.PUBLIC;
                    break;
                case "private":
                    accessLevel = AccessLevel.PRIVATE;
                    break;
                case "protected":
                    accessLevel = AccessLevel.PROTECTED;
                    break;
                default:
                    return new BifyArgumentError($"Invalid flag '{flag.Token.Value}' for class field.")
                        .Throw<AccessLevel>();
            }
        }

        return accessLevel;
    }

    private static void ResolveParameters(AstNode? node, ref List<ParameterInfo> parameters, CodeGenContext ctx)
    {
        if (node == null)
            return;

        if (node is AstParam astParam)
        {
            string name = astParam.Name.Token.Value;

            if (parameters.Any(parameter => parameter.Name == name))
            {
                new BifyNameError($"Duplicated parameter: '{name}'").Throw();
            }

            bool isConst = astParam.Flag is AstFlag flag && flag.HasFlag("const");
            BifyType type = TypeResolver.Resolve(astParam.Type, ctx);

            parameters.Add(new ParameterInfo(name, type, isConst));
            return;
        }

        if (node.Token.Type == TokenType.COMMA)
        {
            ResolveParameters(node.Right, ref parameters, ctx);
            ResolveParameters(node.Left, ref parameters, ctx);
            return;
        }

        throw new ArgumentException("Expected an AstParam node");
    }

    private static void AddFunctionParametersToScope(LLVMValueRef functionValue, List<ParameterInfo> parameters,
        CodeGenContext ctx)
    {
        for (uint i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[(int)i];
            var alloca = ctx.Builder.BuildAlloca(parameter.Type.Type, parameter.Name);
            var paramVal = functionValue.GetParam(i);
            ctx.Builder.BuildStore(paramVal, alloca);
            if (parameter.Name != "this" && parameter.Type is ClassType)
            {
                ctx.HeapControl.BuildRetainStrong(paramVal);
            }
            ctx.CurrentScope.Define(parameter.Name, alloca, parameter.Type, parameter.IsConst);
        }
    }
}