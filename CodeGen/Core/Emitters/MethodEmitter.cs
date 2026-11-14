#nullable enable
using System.Collections.Generic;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Validators;

namespace BifyLang.CodeGen.Core.Emitters;

public class MethodEmitter(CodeGenContext ctx) : CodeGenBase(ctx)
{
    private readonly FunctionBodyEmitter _bodyEmitter = new(ctx);

    
    public void EmitMethod(ClassMethod classMethod)
    {
        var parameters = new List<ParameterInfo>
        {
            new("this", classMethod.DeclaringClass, false)
        };
        parameters.AddRange(classMethod.Parameters);

        var returnType = classMethod.Signature.ReturnType;
        bool isDestructor = classMethod.Name == DestructorSynthesizer.DestructorName;
        bool isConstructor = classMethod.Name == "constructor";

        if (!isConstructor && !isDestructor)
            _ = new FunctionPathChecker(returnType, classMethod.Body);

        _bodyEmitter.Emit(
            classMethod.FunctionValue,
            returnType,
            parameters,
            classMethod.Body,
            isMethod: true,
            isDestructor: isDestructor,
            isConstructor: isConstructor,
            declaringClass: classMethod.DeclaringClass);
    }
}