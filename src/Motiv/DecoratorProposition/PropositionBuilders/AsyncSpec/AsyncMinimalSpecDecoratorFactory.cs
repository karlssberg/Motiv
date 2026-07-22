using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.DecoratorProposition.PropositionBuilders.AsyncSpec;

/// <summary>
/// A factory for renaming an existing asynchronous proposition, mirroring the synchronous
/// <see cref="Motiv.DecoratorProposition.PropositionBuilders.Spec.MinimalSpecDecoratorFactory{TModel,TMetadata}" />.
/// </summary>
/// <param name="spec">The asynchronous proposition to decorate.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
[FluentTarget(typeof(Motiv.Spec), TerminalMethod = TerminalMethod.None)]
public readonly partial struct AsyncMinimalSpecDecoratorFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(AsyncSpecBuildOverloads))]AsyncSpecBase<TModel, TMetadata> spec)
{
    /// <summary>Creates an asynchronous proposition named with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An asynchronous proposition for the model.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="statement"/> is null, empty or whitespace.</exception>
    public AsyncSpecBase<TModel, TMetadata> Create(string statement) =>
        new AsyncMinimalSpecDecoratorProposition<TModel, TMetadata>(
            spec,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement)),
                spec.Description));
}
