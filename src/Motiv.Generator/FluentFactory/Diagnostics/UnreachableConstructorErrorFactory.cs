using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentFactory.Model;
using Motiv.Generator.FluentFactory.Model.Methods;

namespace Motiv.Generator.FluentFactory.Diagnostics;

public class UnreachableConstructorErrorFactory(
    ImmutableHashSet<IFluentMethod> allIgnoredMethods)
{
    private static readonly DiagnosticDescriptor UnreachableConstructorDiagnosticDescriptor = new DiagnosticDescriptor(
        "MOTIV001",
        "Duplicate fluent method",
        "Unreachable constructor '{0}'. There is a signature clash with fluent-builder method '{1}'. {2}. {3}.",
        "FluentBuilder",
        DiagnosticSeverity.Error,
        true);

    private readonly ImmutableHashSet<IMethodSymbol> _allIgnoredMultiMethods = allIgnoredMethods
        .OfType<MultiMethod>()
        .Select(m => m.ParameterConverter)
        .ToImmutableHashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

    private readonly SymbolDisplayFormat _shortFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters |
                         SymbolDisplayGenericsOptions.IncludeTypeConstraints |
                         SymbolDisplayGenericsOptions.IncludeVariance,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters |
                       SymbolDisplayMemberOptions.IncludeType,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType |
                          SymbolDisplayParameterOptions.IncludeName |
                          SymbolDisplayParameterOptions.IncludeDefaultValue,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                              SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private readonly SymbolDisplayFormat _fullFormat = new(
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

    public IEnumerable<Diagnostic> Create(
        IFluentMethod selectedMethod,
        ImmutableArray<IFluentMethod> ignoredMethods)
    {
        if (ignoredMethods.Length == 0)
            yield break;

        var unreachableConstructors = GetUnreachableConstructors(selectedMethod, ignoredMethods);

        foreach (var keyValuePair in unreachableConstructors)
        {
            var unreachableConstructor = keyValuePair.Key;
            var unreachableConstructorMethods = keyValuePair.Value
                .Distinct(FluentMethodSignatureEqualityComparer.Default);

            foreach (var unreachableConstructorMethod in unreachableConstructorMethods)
            {
                var ctorDisplayString = unreachableConstructor.ToDisplayString(_fullFormat);

                foreach (var location in unreachableConstructorMethod.SourceParameter?.Locations ?? [null])
                {
                    yield return Diagnostic.Create(
                        UnreachableConstructorDiagnosticDescriptor,
                        location,
                        ctorDisplayString,
                        selectedMethod.ToDisplayString(),
                        string.Join(". ", GetDetails(unreachableConstructorMethod)),
                        GetSuggestions(selectedMethod, unreachableConstructorMethod));
                    break;
                }
            }
        }
    }

    private IEnumerable<string> GetDetails(IFluentMethod ignoredMethod)
    {
        if (ignoredMethod is RegularMethod or MultiMethod)
            yield return
                $"This involves the constructor parameter '{ignoredMethod.SourceParameter?.ToDisplayString(_shortFormat)}'";

        if (ignoredMethod is MultiMethod multiMethod)
            yield return
                $"The parameter value is obtained from the fluent-method template '{multiMethod.ParameterConverter.ToDisplayString(_fullFormat)}'";
    }

    private IReadOnlyDictionary<ISymbol, Collection<IFluentMethod>> GetUnreachableConstructors(
        IFluentMethod selectedMethod, ImmutableArray<IFluentMethod> ignoredMethods)
    {
        var dictionary = new Dictionary<ISymbol, Collection<IFluentMethod>>(SymbolEqualityComparer.Default);

        foreach (var ignoredMethod in ignoredMethods)
        foreach (var constructor in selectedMethod.FindUnreachableConstructors(ignoredMethod, _allIgnoredMultiMethods))
        {
            if (dictionary.TryGetValue(constructor, out var methods))
            {
                methods.Add(ignoredMethod);
                continue;
            }

            dictionary.Add(constructor, [ignoredMethod]);
        }

        return dictionary;
    }

    private string GetSuggestions(IFluentMethod selectedMethod, IFluentMethod ignoredMethod)
    {
        return ignoredMethod switch
        {
            RegularMethod regularMethod =>
                $"Try changing the fluent method name '{regularMethod.Name}', or constructor parameter type '{regularMethod.SourceParameter?.ToDisplayString(_fullFormat)}'",

            MultiMethod multiMethod when !SymbolEqualityComparer.Default.Equals(selectedMethod.SourceParameter?.Type,
                    multiMethod.SourceParameter.Type) =>
                $"The issue is with the fluent-method template '{multiMethod.ParameterConverter.ToDisplayString(_fullFormat)}'. "
                + "The clashing methods have differing return-types, which is caused by different constructor parameter types. "
                + "Try removing or renaming the template method, or changing its signature",
            MultiMethod multiMethod =>
                $"The issue is with the fluent-method template '{multiMethod.ParameterConverter.ToDisplayString(_fullFormat)}'. "
                + "Try removing or renaming the template method, or changing its signature",
            _ =>
                "Try changing changing one of the prior fluent-method signatures/name to avoid the conflict"
        };
    }
}
