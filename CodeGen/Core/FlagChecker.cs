using System.Collections.Generic;
using System.Linq;

namespace BifyLang.CodeGen.Core;

public static class FlagChecker
{
    private static readonly HashSet<string> AllowedFunctionFlags =
    [
        "public",
        "private"
    ];

    private static readonly HashSet<string> AllowedMethodFlags =
    [
        "public",
        "private",
        "protected",
        "virtual",
        "override"
    ];

    private static readonly HashSet<string> AllowedAttributeFlags =
    [
        "public",
        "private",
        "protected",
        "weak",
        "const"
    ];

    private static readonly HashSet<string> AllowedClassFlags =
    [
        "public",
        "private"
    ];

    private static readonly HashSet<string> AllowedVariableFlags =
    [
        "const",
        "weak"
    ];

    private static readonly HashSet<string> AllowedParameterFlags =
    [
        "const"
    ];

    public static bool CheckFunctionFlags(
        IEnumerable<string> flags,
        out string? error)
    {
        return Check(
            flags,
            AllowedFunctionFlags,
            "function",
            out error
        );
    }

    public static bool CheckMethodFlags(
        IEnumerable<string> flags,
        out string? error)
    {
        if (!Check(flags, AllowedMethodFlags, "method", out error))
            return false;

        var flagList = flags.ToList();
        if (flagList.Contains("virtual") && flagList.Contains("override"))
        {
            error = "Method cannot be both 'virtual' and 'override'.";
            return false;
        }

        return true;
    }

    public static bool CheckAttributeFlags(
        IEnumerable<string> flags,
        out string? error)
    {
        return Check(
            flags,
            AllowedAttributeFlags,
            "attribute",
            out error
        );
    }

    public static bool CheckClassFlags(
        IEnumerable<string> flags,
        out string? error)
    {
        return Check(
            flags,
            AllowedClassFlags,
            "class",
            out error
        );
    }

    public static bool CheckVariableFlags(
        IEnumerable<string> flags,
        out string? error)
    {
        return Check(
            flags,
            AllowedVariableFlags,
            "variable",
            out error
        );
    }

    public static bool CheckParameterFlags(
        IEnumerable<string> flags,
        out string? error)
    {
        return Check(
            flags,
            AllowedParameterFlags,
            "parameter",
            out error
        );
    }

    private static bool Check(
        IEnumerable<string> flags,
        HashSet<string> allowedFlags,
        string target,
        out string? error)
    {
        error = null;

        var flagList = flags.ToList();

        foreach (var flag in flagList)
        {
            if (!allowedFlags.Contains(flag))
            {
                error = $"Flag '{flag}' is not allowed on {target}.";
                return false;
            }
        }

        var duplicate = flagList
            .GroupBy(x => x)
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicate != null)
        {
            error = $"Flag '{duplicate.Key}' is specified multiple times.";
            return false;
        }

        var accessModifierCount = flagList.Count(flag =>
            flag is "public" or "private" or "protected"
        );

        if (accessModifierCount > 1)
        {
            error =
                $"Only one access modifier can be specified on {target}.";

            return false;
        }

        error = null;
        return true;
    }
}