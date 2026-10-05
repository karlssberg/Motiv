using Microsoft.CodeAnalysis;

namespace Motiv.Explain;

internal static class ExplainDiagnostics
{
    private const string Category = "Motiv.Explain";

    public static readonly DiagnosticDescriptor UsedAsDelegate = new(
        "MOTIV1001",
        "An [Explain] method used as a delegate is not explained",
        "'{0}' is used here as a delegate, so calls through it will not be explained",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Explain builds replace direct calls only. A method group, such as one passed to LINQ, " +
                     "still runs the original method and logs nothing.");

    public static readonly DiagnosticDescriptor CalledFromAnotherProject = new(
        "MOTIV1002",
        "An [Explain] method called from another project is not explained",
        "'{0}' is declared in another project, so this call will not be explained",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Explain builds can replace calls only within the project that declares the method.");

    public static readonly DiagnosticDescriptor TypeNotPartial = new(
        "MOTIV1003",
        "An [Explain] method's type must be partial",
        "'{0}' can't be explained because '{1}' is not partial",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The decomposed specification is generated inside the method's type, so it can read the " +
                     "same private members the method reads. That needs the type, and every type around it, " +
                     "to be partial.");

    public static readonly DiagnosticDescriptor CannotExplain = new(
        "MOTIV1004",
        "An [Explain] method can't be explained",
        "'{0}' can't be explained: {1}",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Explain builds replace calls to expression-bodied boolean methods. Calls to this method " +
                     "will run unchanged.");
}
