using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentFactory.Analysis;
using Motiv.Generator.FluentFactory.Model.Storage;

namespace Motiv.Generator.FluentFactory.Model;

[DebuggerDisplay("{ToDisplayString()}")]
public class ConstructorMetadata(FluentConstructorContext constructorContext)
{
    public IMethodSymbol Constructor { get; set; } = constructorContext.Constructor;

    public IList<IMethodSymbol> CandidateConstructors { get; } = [constructorContext.Constructor];

    public FluentFactoryGeneratorOptions Options { get; set; } = constructorContext.Options;

    public OrderedDictionary<IParameterSymbol, IFluentValueStorage> ValueStorage { get; } =
        constructorContext.ValueStorage;
        
    public FluentConstructorContext Context { get; } = constructorContext;

    public ConstructorMetadata Clone()
    {
        return new ConstructorMetadata(Context);
    }

    public string ToDisplayString() => Constructor.ToDisplayString();
}
