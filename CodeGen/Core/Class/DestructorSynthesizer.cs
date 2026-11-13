#nullable enable
using System.Collections.Generic;
using BifyLang.CodeGen.Types;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public static class DestructorSynthesizer
{
    public const string DestructorName = "destructor";

    public static ClassMethod CreateDefault(CodeGenContext ctx, ClassType classType)
    {
        var returnType = BifyType.CreateVoid(ctx);
        var paramTypes = new[] { LLVMTypeRef.CreatePointer(ctx.Context.Int8Type, 0) };
        var funcType = LLVMTypeRef.CreateFunction(returnType.Type, paramTypes, false);
        var mangledName = NameMangler.GenerateMethodName(ctx.ModuleContext.Name, classType.Name,
            DestructorSynthesizer.DestructorName);
        var signature = new CallableType(DestructorSynthesizer.DestructorName, mangledName, [], returnType, funcType);

        return new ClassMethod(
            name: DestructorName,
            signature: signature,
            ctx.Module.AddFunction(mangledName,funcType),
            accessLevel: AccessLevel.PUBLIC,
            vtableIndex: 0,
            declaringClass: classType,
            body: null,
            isVirtual: true,
            isOverride: false,
            parameters: []
        );
    }
}
