using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentFactory.Model.Methods;
using Motiv.Generator.FluentFactory.Model.Steps;

namespace Motiv.Generator.FluentFactory.Model;

public static class FluentMethodExtensions
{
    private static readonly SymbolDisplayFormat _fullFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters |
                         SymbolDisplayGenericsOptions.IncludeTypeConstraints |
                         SymbolDisplayGenericsOptions.IncludeVariance,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters |
                       SymbolDisplayMemberOptions.IncludeContainingType,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType |
                          SymbolDisplayParameterOptions.IncludeName |
                          SymbolDisplayParameterOptions.IncludeDefaultValue,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                              SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static string ToDisplayString(this IFluentMethod method) =>
        method switch
        {
            MultiMethod multiMethod => multiMethod.ParameterConverter.ToDisplayString(_fullFormat),
            _ => method.SerializeFluentMethod()
        };

    private static string SerializeFluentMethod(this IFluentMethod method)
    {
        var typeParameterDisplayStrings = method.TypeParameters
            .Select(fluentTypeParameter => fluentTypeParameter.TypeParameterSymbol.ToDisplayString(_fullFormat));

        var typeParameterList = method.TypeParameters.Length > 0
            ? $"<{string.Join(", ", typeParameterDisplayStrings)}>"
            : string.Empty;

        var parameterDisplayStrings = method.MethodParameters
            .Select(p => p.ParameterSymbol.ToDisplayString(_fullFormat));

        return $"{method.Name}{typeParameterList}({string.Join(", ", parameterDisplayStrings)})";
    }

    public static IEnumerable<IMethodSymbol> FindUnreachableConstructors(
        this IFluentMethod selectedMethod,
        IFluentMethod ignoredMethod,
        IEnumerable<IMethodSymbol> allIgnoredMultiMethods)
    {
        var reachableConstructors = (selectedMethod, ignoredMethod) switch
        {
            (_, MultiMethod multiMethod) when multiMethod.SiblingMultiMethods.IsSubsetOf(allIgnoredMultiMethods) =>
                selectedMethod.Return.CandidateConstructors,
            (_, MultiMethod multiMethod) =>
                [..selectedMethod.Return.CandidateConstructors, ..multiMethod.Return.CandidateConstructors],
            ({ Return: TargetTypeReturn targetTypeReturn }, _) =>
                [targetTypeReturn.Constructor],
            _ => selectedMethod.Return.CandidateConstructors
        };

        return ignoredMethod.Return.CandidateConstructors
            .Except<IMethodSymbol>(reachableConstructors, SymbolEqualityComparer.Default);
    }
}
