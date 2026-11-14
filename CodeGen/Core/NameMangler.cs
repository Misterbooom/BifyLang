using System;

namespace BifyLang.CodeGen.Core;
public static class NameMangler
{
    public static string GenerateClassName(string moduleName, string className)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);
        ArgumentException.ThrowIfNullOrEmpty(className);

        return $"{moduleName}.{className}";
    }

    public static string GenerateMethodName(string moduleName, string className, string methodName)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);
        ArgumentException.ThrowIfNullOrEmpty(className);
        ArgumentException.ThrowIfNullOrEmpty(methodName);

        return $"{moduleName}.{className}_{methodName}";
    }

    public static string GenerateFunctionName(string moduleName, string funcName)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);
        ArgumentException.ThrowIfNullOrEmpty(funcName);

        if (moduleName == "main" && funcName == "main")
            return "main";

        return $"{moduleName}.{funcName}";
    }
    public static string GenerateVTable(string moduleName, string className)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);
        ArgumentException.ThrowIfNullOrEmpty(className);

        return $"{moduleName}.{className}.vtable";
    }
}