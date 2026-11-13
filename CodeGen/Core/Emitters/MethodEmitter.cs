#nullable enable
using System.Collections.Generic;
using BifyLang.CodeGen.Types;

namespace BifyLang.CodeGen.Core.Emitters;

public class MethodEmitter(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;
    private readonly FunctionBodyEmitter _bodyEmitter = new(ctx);

    public void EmitMethod(ClassMethod classMethod)
    {
        string symbol = $"{classMethod.DeclaringClass}_{classMethod.Name}";
        var parameters = new List<ParameterInfo>
        {
            new("this", classMethod.DeclaringClass, false)
        };
        parameters.AddRange(classMethod.Parameters);

        var returnType = new BifyType(classMethod.Signature.Name, classMethod.Signature.Type.ReturnType);
        bool isDestructor = classMethod.Name == DestructorSynthesizer.DestructorName;
        _bodyEmitter.Emit(classMethod.FunctionValue, returnType, parameters, classMethod.Body, isMethod: true, isDestructor: isDestructor, declaringClass: classMethod.DeclaringClass);
    }
}