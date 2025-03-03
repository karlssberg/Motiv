using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.Analysis;
using Motiv.Generator.FluentBuilder.Model.Storage;

namespace Motiv.Generator.FluentBuilder.Model;

public class ConstructorMetadata(FluentConstructorContext constructorContext)
{
    public IMethodSymbol Constructor { get; set; } = constructorContext.Constructor;

    public IList<IMethodSymbol> CandidateConstructors { get; } = [constructorContext.Constructor];

    public FluentFactoryGeneratorOptions Options { get; set; } = constructorContext.Options;

    public OrderedDictionary<IParameterSymbol, IFluentValueStorage> ValueStorage { get; } =
        constructorContext.ValueStorage;

    public ConstructorMetadata Clone()
    {
        return new ConstructorMetadata(constructorContext);
    }
}
