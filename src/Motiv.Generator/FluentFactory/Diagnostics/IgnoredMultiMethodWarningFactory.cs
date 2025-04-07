using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentFactory.Model;
using Motiv.Generator.FluentFactory.Model.Methods;
using Motiv.Generator.FluentFactory.Model.Steps;

namespace Motiv.Generator.FluentFactory.Diagnostics;

public class IgnoredMultiMethodWarningFactory(
    ImmutableHashSet<IFluentMethod> allIgnoredMethods)
{
    private static readonly DiagnosticDescriptor IgnoredMultiMethodDiagnosticDescriptor = new (
        "MOTIV002",
        "Ignoring fluent-method-template",
        "Ignoring fluent-method-template '{0}'. " +
            "It is being used by the parameter '{1}' in the constructor '{2}'. Instead, {3}.",
        "FluentBuilder",
        DiagnosticSeverity.Warning,
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

    public IEnumerable<Diagnostic> Create(IFluentMethod selectedMethod, ImmutableArray<MultiMethod> ignoredMultiMethods)
    {
        switch (selectedMethod)
        {
            case RegularMethod when DoIgnoredMethodsCollectivelyCauseUnreachableConstructors():
            case MultiMethod selectedMultiMethod when AreSelectedAndIgnoredMethodsFromSameContainingType(selectedMultiMethod):
                yield break;
        }

        foreach (var ignoredMethod in ignoredMultiMethods)
        {
            var ctorDisplayString = ignoredMethod.ToDisplayString();

            foreach (var location in ignoredMethod.SourceParameter.Locations)
            {
                yield return Diagnostic.Create(
                    IgnoredMultiMethodDiagnosticDescriptor,
                    location,
                    ctorDisplayString,
                    ignoredMethod.SourceParameter.ToDisplayString(_shortFormat),
                    ignoredMethod.SourceParameter.ContainingSymbol.ToDisplayString(_fullFormat),
                    GetSelectedMethodDescription());
                break;
            }
        }

        yield break;

        bool DoIgnoredMethodsCollectivelyCauseUnreachableConstructors() =>
            ignoredMultiMethods
                .All(ignoredMethod =>
                    selectedMethod.FindUnreachableConstructors(ignoredMethod, _allIgnoredMultiMethods).Any());

        bool AreSelectedAndIgnoredMethodsFromSameContainingType(MultiMethod selectedMultiMethod) =>
            ignoredMultiMethods.All(ignoredMethod => SymbolEqualityComparer.Default
                .Equals(
                    ignoredMethod.ParameterConverter.ContainingType,
                    selectedMultiMethod.ParameterConverter.ContainingType));

        string GetSelectedMethodDescription()
        {
            return $"the parameter '{selectedMethod.SourceParameter?.ToDisplayString(_shortFormat)}' in " +
                   $"the constructor '{selectedMethod.SourceParameter?.ContainingSymbol.ToDisplayString(_fullFormat)}' " +
                   $"was used as the basis for the fluent method. " +
                   "Perhaps the ignored method-template can be removed or modified.";
        }
    }
}
