#nullable enable
using System.Collections.Generic;
using BifyLang.CodeGen.Types;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public static class DestructorSynthesizer
{
    public const string DestructorName = "destructor";

    public static ClassMethod CreateDefault(CodeGenContext ctx, string className)
    {
        var returnType = BifyType.CreateVoid(ctx);
        var paramTypes = new[] { LLVMTypeRef.CreatePointer(ctx.Context.Int8Type, 0) };
        var funcType = LLVMTypeRef.CreateFunction(returnType.Type, paramTypes, false);
        var signature = new BifyType(returnType.Name, funcType);

        return new ClassMethod(
            name: DestructorName,
            signature: signature,
            ctx.Module.AddFunction(className + "_" + DestructorName, funcType),
            accessLevel: AccessLevel.PUBLIC,
            vtableIndex: 0,
            declaringClass: className,
            body: null,
            isVirtual: true,
            isOverride: false,
            parameters: []
        );
    }
}
