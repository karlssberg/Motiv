using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentFactory.Model;
using Motiv.Generator.FluentFactory.Model.Methods;

namespace Motiv.Generator.FluentFactory.Diagnostics;

public class IgnoredMultiMethodWarningFactory(
    ImmutableHashSet<IFluentMethod> allIgnoredMethods)
{

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

            yield return Diagnostic.Create(
                FluentFactoryGenerator.ContainsSupersededFluentMethodTemplate,
                ignoredMethod.SourceParameter.GetAttribute(TypeName.MultipleFluentMethodsAttribute)?.GetLocationAtIndex(0)
                    ?? ignoredMethod.SourceParameter.Locations.FirstOrDefault(),
                selectedMethod.SourceParameter?.Locations,
                ctorDisplayString,
                ignoredMethod.SourceParameter.ToDisplayString(_shortFormat),
                ignoredMethod.SourceParameter.ContainingSymbol.ToFullDisplayString(),
                GetSelectedMethodDescription());

            foreach (var location in ignoredMethod.ParameterConverter.Locations)
            {
                yield return Diagnostic.Create(
                    FluentFactoryGenerator.FluentMethodTemplateSuperseded,
                    location,
                    ignoredMethod.ParameterConverter.ToFullDisplayString(),
                    ignoredMethod.SourceParameter.ToFullDisplayString(),
                    ignoredMethod.SourceParameter.ContainingSymbol.ToFullDisplayString(),
                    selectedMethod.SourceParameter?.ToFullDisplayString(),
                    selectedMethod.SourceParameter?.ContainingSymbol.ToFullDisplayString());
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
                   $"the constructor '{selectedMethod.SourceParameter?.ContainingSymbol.ToFullDisplayString()}' " +
                   $"was used as the basis for the fluent method. " +
                   "Perhaps the ignored method-template can be removed or modified.";
        }
    }
}
