using Microsoft.CodeAnalysis;

namespace Motiv.Generator.FluentFactory;

public static class MotivDiagnosticDescriptor
{
    private const string Category = "FluentFactory";

    public static readonly DiagnosticDescriptor UnreachableConstructor = new(
        id: "MOTIV001",
        title: "Unreachable fluent constructor",
        messageFormat:
        "Unreachable fluent constructor '{0}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    public static readonly DiagnosticDescriptor ContainsSupersededFluentMethodTemplate = new(
        id: "MOTIV002",
        title: "Multiple fluent method contains superseded method",
        messageFormat: "Ignoring fluent-method-template '{0}', used by the parameter '{1}' in the constructor '{2}'. Instead, {3}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    public static readonly DiagnosticDescriptor IncompatibleFluentMethodTemplate = new(
        id: "MOTIV003",
        title: "Fluent method template not compatible",
        category: Category,
        messageFormat: "Incompatible return type to the method '{0}'. It is not assignable to the fluent constructor parameter '{1}'.",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    public static readonly DiagnosticDescriptor AllFluentMethodTemplatesIncompatible = new(
        id: "MOTIV004",
        title: "All fluent method template incompatible",
        category: Category,
        messageFormat: "None of the fluent-method-templates have return types that are assignable to the fluent constructor parameter '{0}'",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    public static readonly DiagnosticDescriptor FluentMethodTemplateAttributeNotStatic = new(
        id: "MOTIV005",
        title: "Fluent method template not static",
        category: Category,
        messageFormat: "Static method required '{0}'",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor FluentMethodTemplateNotCompatible = new(
        id: "MOTIV006",
        title: "Fluent method template not compatible",
        category: Category,
        messageFormat: "Fluent method template's return type '{0}' is not assignable to the fluent constructor parameter '{1}' in constructor '{2}'",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor FluentMethodTemplateSuperseded = new(
        id: "MOTIV007",
        title: "Fluent method template superseded",
        category: Category,
        messageFormat: "Fluent method template '{0}' is not being applied for the fluent constructor parameter '{1}' in constructor '{2}'. " +
            "This is because of the higher precedence afforded to fluent constructor parameter '{3}' in constructor '{4}'.",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);
}
