using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.FluentFactory.Generator.Generation;

namespace Motiv.FluentFactory.Generator.Model;

public static class SymbolExtensions
{
    private static readonly SymbolDisplayFormat FullFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters |
                         SymbolDisplayGenericsOptions.IncludeTypeConstraints,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters |
                       SymbolDisplayMemberOptions.IncludeContainingType |
                       SymbolDisplayMemberOptions.IncludeType,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType |
                          SymbolDisplayParameterOptions.IncludeName |
                          SymbolDisplayParameterOptions.IncludeDefaultValue,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                              SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static bool IsPartial(this ITypeSymbol typeSymbol)
    {
        return typeSymbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
    }

    public static string GetFluentMethodName(this IParameterSymbol parameterSymbol)
    {
        var regularMethodAttribute = parameterSymbol.GetAttribute(TypeName.FluentMethodAttribute);
        var multipleMethodAttribute = parameterSymbol.GetAttribute(TypeName.MultipleFluentMethodsAttribute);

        var name = (regularMethodAttribute, mutlipleMethodAttribute: multipleMethodAttribute) switch
        {
            ({ ConstructorArguments: { Length: 1 } args }, _) when args.First().Value is not null =>
                args.First().Value!.ToString(),
            (_, { ConstructorArguments: { Length: 1 } args }) when args.First().Value is INamedTypeSymbol =>
                args.First().Value!.ToString(),
            _ => $"With{parameterSymbol.Name.Capitalize()}"
        };

        return name;
    }

    public static IEnumerable<(IMethodSymbol Method, ICollection<Diagnostic> Diagnostics)> GetMultipleFluentMethodSymbols(
        this Compilation compilation,
        IParameterSymbol parameterSymbol)
    {
        var attribute = parameterSymbol.GetAttribute(TypeName.MultipleFluentMethodsAttribute);

        var methodTemplateClass = attribute?.ConstructorArguments.FirstOrDefault();
        if (methodTemplateClass?.Value is not ITypeSymbol methodTemplateClassSymbol)
            return [];

        var multiMethodClass = methodTemplateClassSymbol.IsOpenGenericType()
            ? methodTemplateClassSymbol.OriginalDefinition
            : methodTemplateClassSymbol;

        var attributeSyntax = attribute?.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
        var location = attributeSyntax?.ArgumentList?.Arguments.FirstOrDefault()?.GetLocation()
                       ?? parameterSymbol.Locations.FirstOrDefault();

        return multiMethodClass
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method => !method.IsImplicitlyDeclared)
            .Where(method => method.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == TypeName.FluentMethodTemplateAttribute))
            .Select(method =>
            {
                List<Diagnostic> diagnostics = [];

                if (!method.IsStatic)
                    diagnostics.Add(Diagnostic.Create(
                        FluentFactoryGenerator.FluentMethodTemplateAttributeNotStatic,
                        location,
                        method.Locations,
                        method.ToFullDisplayString(),
                        methodTemplateClassSymbol.ToFullDisplayString()));

                if (!compilation.IsAssignable(method.ReturnType.OriginalDefinition, parameterSymbol.Type))
                {
                    diagnostics.AddRange(
                    [
                        Diagnostic.Create(
                            FluentFactoryGenerator.IncompatibleFluentMethodTemplate,
                            location,
                            method.Locations,
                            ImmutableDictionary.Create<string, string?>()
                                .Add("FluentMethodTemplate", method.ToFullDisplayString())
                                .Add("FluentConstructorParameter", parameterSymbol.ToFullDisplayString()),
                            method.ToFullDisplayString(),
                            parameterSymbol.ToFullDisplayString())
                    ]);
                }

                return (method, diagnostics as ICollection<Diagnostic>);
            });
    }

    public static Location? GetLocationAtIndex(this AttributeData attributeData, int argumentIndex)
    {
        var attributeSyntax = attributeData.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
        return attributeSyntax?.ArgumentList?.Arguments.ElementAt(argumentIndex).GetLocation();
    }

    public static string ToFullDisplayString(this ISymbol symbol)
    {
        return symbol.ToDisplayString(FullFormat);
    }

    public  static AttributeData? GetAttribute(
        this IParameterSymbol parameterSymbol,
        TypeName fluentMethodName)
    {
        var format = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.None
        );

        return parameterSymbol
            .GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString(format) == fluentMethodName);
    }

    public static INamedTypeSymbol ReplaceTypeParameters(
        this INamedTypeSymbol type,
        ImmutableDictionary<ITypeParameterSymbol, ITypeSymbol> replacements)
    {
        if (!type.IsGenericType)
            return type;

        var newTypeArgs = type.TypeArguments.Select(arg =>
            arg is ITypeParameterSymbol tp && replacements.TryGetValue(tp, out var replacement)
                ? replacement
                : arg is INamedTypeSymbol namedArg
                    ? ReplaceTypeParameters(namedArg, replacements)
                    : arg);

        return type.OriginalDefinition.Construct(newTypeArgs.ToArray());
    }

    public static bool CanBeCustomStep(this INamedTypeSymbol containingType)
    {
        var isPartial = containingType.IsPartial();
        var isStatic = containingType.IsStatic;
        return isPartial && !isStatic;
    }

    public static int GetFluentMethodPriority(this IParameterSymbol parameterSymbol)
    {
        const string priorityPropertyName = nameof(FluentMethodAttribute.Priority);

        var attribute = parameterSymbol.GetAttribute(TypeName.MultipleFluentMethodsAttribute)
            ?? parameterSymbol.GetAttribute(TypeName.FluentMethodAttribute);
        if (attribute == null) return 0; // Default priority for parameters without the attribute

        // Look for Priority named argument
        var priorityArg = attribute.NamedArguments
            .FirstOrDefault(na => na.Key == priorityPropertyName);

        return priorityArg.Value switch
        {
            { Value: int value, IsNull: false } => value,
            _ => 0
        };
    }
}
