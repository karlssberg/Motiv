using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentFactory.Model.Storage;

namespace Motiv.Generator.FluentFactory.Model.Steps;

public interface IFluentReturn
{
    string IdentifierDisplayString(INamespaceSymbol currentNamespace);

    string IdentifierDisplayString(INamespaceSymbol currentNamespace, IDictionary<FluentType, ITypeSymbol> genericTypeArgumentMap);

    INamespaceSymbol Namespace { get; }

    /// <summary>
    /// The known constructor parameters up until this step.
    /// Potentially more parameters are required to satisfy a constructor signature.
    /// </summary>
    ParameterSequence KnownConstructorParameters { get; }

    ImmutableArray<IParameterSymbol> GenericConstructorParameters { get; }

    OrderedDictionary<IParameterSymbol, IFluentValueStorage> ValueStorage { get; }

    ImmutableArray<IMethodSymbol> CandidateConstructors { get; }
}
