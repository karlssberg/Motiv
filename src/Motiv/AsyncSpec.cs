namespace Motiv;

/// <summary>Represents an asynchronous proposition that yields custom metadata based on the outcome of the underlying spec/predicate.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class AsyncSpec<TModel, TMetadata> : AsyncSpecBase<TModel, TMetadata>
{
    private readonly AsyncSpecBase<TModel, TMetadata> _spec;

    /// <summary>Initializes a new instance of the AsyncSpec class with an AsyncSpecBase instance.</summary>
    /// <param name="spec">The base proposition associated with the AsyncSpec instance.</param>
    protected AsyncSpec(AsyncSpecBase<TModel, TMetadata> spec)
    {
        spec.ThrowIfNull(nameof(spec));

        _spec = spec;
        Description = spec.Description;
    }

    /// <summary>Initializes a new instance of the AsyncSpec class with a specification factory.</summary>
    /// <param name="specificationFactory">The specification factory to create the AsyncSpec instance.</param>
    protected AsyncSpec(Func<AsyncSpecBase<TModel, TMetadata>> specificationFactory)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));

        _spec = specificationFactory().ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        Description = _spec.Description;
    }

    /// <summary>Gets the underlying specifications that make up this composite proposition.</summary>
    public override IEnumerable<SpecBase> Underlying => _spec.Underlying;

    /// <summary>Gets the description of the proposition.</summary>
    public override ISpecDescription Description { get; }

    /// <inheritdoc />
    public override Task<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        _spec.MatchesAsync(model, cancellationToken);

    /// <summary>Asynchronously determines whether the specified model satisfies the proposition.</summary>
    /// <param name="model">The model to be checked against the proposition.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>A BooleanResultBase containing the result of the proposition check and the associated metadata.</returns>
    protected override Task<BooleanResultBase<TMetadata>> EvaluateSpecAsync(TModel model, CancellationToken cancellationToken) =>
        _spec.EvaluateAsync(model, cancellationToken);
}

/// <summary>
/// Represents an asynchronous proposition that defines a condition for a model of type TModel. This
/// proposition is associated with a string metadata.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public class AsyncSpec<TModel> : AsyncSpecBase<TModel, string>
{
    private readonly AsyncSpecBase<TModel, string> _spec;

    /// <summary>Initializes a new instance of the AsyncSpec class with an AsyncSpecBase instance.</summary>
    /// <param name="spec">The base proposition associated with the AsyncSpec instance.</param>
    protected AsyncSpec(AsyncSpecBase<TModel, string> spec)
    {
        spec.ThrowIfNull(nameof(spec));

        _spec = spec;
        Description = spec.Description;
    }

    /// <summary>Initializes a new instance of the AsyncSpec class with a specification factory.</summary>
    /// <param name="specFactory">The specification factory to create the AsyncSpec instance.</param>
    protected AsyncSpec(Func<AsyncSpecBase<TModel, string>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));

        _spec = specFactory().ThrowIfFactoryOutputIsNull(nameof(specFactory));
        Description = _spec.Description;
    }

    /// <summary>Gets the description of the proposition.</summary>
    public override ISpecDescription Description { get; }

    /// <summary>Gets the underlying specifications that make up this composite proposition.</summary>
    public override IEnumerable<SpecBase> Underlying => _spec.Underlying;

    /// <inheritdoc />
    public override Task<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        _spec.MatchesAsync(model, cancellationToken);

    /// <summary>Asynchronously determines whether the specified model satisfies the proposition.</summary>
    /// <param name="model">The model to be checked against the proposition.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>A BooleanResultBase containing the result of the proposition being applied to a model.</returns>
    protected override Task<BooleanResultBase<string>> EvaluateSpecAsync(TModel model, CancellationToken cancellationToken) =>
        _spec.EvaluateAsync(model, cancellationToken);
}
