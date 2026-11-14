using System;

namespace BifyLang.Exceptions;

public enum DiagnosticKind
{
    Type,
    Syntax,
    Name,
    Attribute,
    Cast,
    Arithmetic,
    Overflow,
    Import,
    CircularInheritance,
    Unknown,
}

public sealed record SourceLocation(
    string FilePath,
    int LineNumber,
    int ColumnNumber = 0,
    string? ModuleName = null,
    string? FunctionName = null
);

public sealed class BifyCompilationException : Exception
{
    public Diagnostic? Diagnostic { get; }
    public bool IsPoisonAbort => Diagnostic == null;

    public BifyCompilationException(Diagnostic diagnostic) : base(diagnostic.Message)
    {
        Diagnostic = diagnostic;
    }

    public BifyCompilationException() : base("Poisoned symbol encountered")
    {
    }
}

public sealed record Diagnostic
(
    string Message,
    DiagnosticKind Kind,
    SourceLocation Location
);
