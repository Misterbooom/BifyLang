using System;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Core.Emitters;
using BifyLang.CodeGen.Types;

namespace BifyLang.CodeGen.Declarations;

public class ClassGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public void EmitClass(AstClass classNode)
    {
        if (!_ctx.TypeRegistry.TryGetType(classNode.NameNode.Token.Value, out var type))
        {
            throw new Exception($"Cannot resolve type '{classNode.NameNode.Token.Value}' from TypeRegistry. ");
        }

        ClassType classType = (ClassType)type;
        MethodEmitter methodEmitter = new(_ctx);

        foreach (var method in classType.Methods.Values)
        {
            if (method.DeclaringClass == classType.Name)
            {
                methodEmitter.EmitMethod(method);
            }
        }

        VTableBuilder.Build(classType.VTable, _ctx, classNode.NameNode.Token.Value);
        classType.VTableGlobal = classType.VTable.GlobalValue;
    }
}