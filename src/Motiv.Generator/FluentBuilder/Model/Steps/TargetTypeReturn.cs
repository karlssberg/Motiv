using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.Generation;
using Motiv.Generator.FluentBuilder.Model.Storage;

namespace Motiv.Generator.FluentBuilder.Model.Steps;

public class TargetTypeReturn(
    IMethodSymbol targetTypeConstructor,
    ParameterSequence knownConstructorParameters) : IFluentReturn
{
    public ImmutableArray<IParameterSymbol> GenericConstructorParameters { get; } =
    [
        ..knownConstructorParameters
            .Where(parameter => parameter.Type.IsOpenGenericType())
    ];

    public OrderedDictionary<IParameterSymbol, IFluentValueStorage> ValueStorage { get; set; }

    public ImmutableArray<IMethodSymbol> CandidateConstructors => [targetTypeConstructor];
    public string IdentifierDisplayString(INamespaceSymbol currentNamespace)
    {
        return IdentifierDisplayString(currentNamespace, new Dictionary<FluentType, ITypeSymbol>());
    }

    public string IdentifierDisplayString(
        INamespaceSymbol currentNamespace,
        IDictionary<FluentType, ITypeSymbol> genericTypeArgumentMap)
    {
        var allArgs = targetTypeConstructor.ContainingType.TypeParameters
            .Select(typeParameter => genericTypeArgumentMap.TryGetValue(new FluentType(typeParameter), out var type)
                ? type
                : typeParameter)
            .ToArray();

        var constructedType = allArgs.Length > 0
            ? targetTypeConstructor.ContainingType.Construct(allArgs)
            : targetTypeConstructor.ContainingType;

        return constructedType.ToDynamicDisplayString(currentNamespace);
    }

    public INamespaceSymbol Namespace => targetTypeConstructor.ContainingNamespace;
    public ParameterSequence KnownConstructorParameters { get; } = knownConstructorParameters;
}
