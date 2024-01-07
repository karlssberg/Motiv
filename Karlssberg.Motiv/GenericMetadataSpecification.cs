namespace Karlssberg.Motiv;

/// <summary>
/// Represents a predicate that when evaluated returns a boolean result with associated metadata and description of the underlying specification that were responsible for the result.
/// </summary>
/// <typeparam name="T">The type of the input parameter.</typeparam>
/// <typeparam name="TResult">The type of the return value.</typeparam>
/// <returns>The return value.</returns>
internal sealed class GenericMetadataSpecification<TModel, TMetadata>(
    string description,
    Func<TModel, bool> predicate,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse) : SpecificationBase<TModel, TMetadata>
{
    private readonly Func<TModel, bool> _predicate = predicate.ThrowIfNull(nameof(predicate));
    private readonly Func<TModel, TMetadata> _whenTrue = whenTrue.ThrowIfNull(nameof(whenTrue));
    private readonly Func<TModel, TMetadata> _whenFalse = whenFalse.ThrowIfNull(nameof(whenFalse));

    /// <summary>
    /// Gets or sets the description of the specification.
    /// </summary>
    public override string Description => description.ThrowIfNullOrWhitespace(nameof(description));

    /// <summary>
    /// Determines if the specified model satisfies the specification.
    /// </summary>
    /// <param name="model">The model to be evaluated.</param>
    /// <returns>A BooleanResultBase object containing the result of the evaluation.</returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        return WrapException.IfIsSatisfiedByInvocationFails(this,
            () =>
            {
                var isSatisfied = InvokePredicate(model);

                var cause = isSatisfied
                    ? InvokeWhenTrue(model)
                    : InvokeWhenFalse(model);

                return new BooleanResult<TMetadata>(isSatisfied, cause, description);
            });
    }

    private bool InvokePredicate(TModel model) =>
        WrapException.IfCallbackInvocationFails(
            this,
            () => _predicate(model),
            nameof(predicate));

    private TMetadata InvokeWhenTrue(TModel model) =>
        WrapException.IfCallbackInvocationFails(
            this,
            () => _whenTrue(model),
            nameof(whenTrue));

    private TMetadata InvokeWhenFalse(TModel model) =>
        WrapException.IfCallbackInvocationFails(
            this,
            () => _whenFalse(model),
            nameof(whenFalse));
}