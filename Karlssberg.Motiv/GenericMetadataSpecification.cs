namespace Karlssberg.Motiv;

internal sealed class GenericMetadataSpecification<TModel, TMetadata>(
    string description,
    Func<TModel, bool> predicate,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse) : SpecificationBase<TModel, TMetadata>
{
    private readonly Func<TModel, bool> _predicate = predicate.ThrowIfNull(nameof(predicate));
    private readonly Func<TModel, TMetadata> _whenTrue = whenTrue.ThrowIfNull(nameof(whenTrue));
    private readonly Func<TModel, TMetadata> _whenFalse = whenFalse.ThrowIfNull(nameof(whenFalse));

    internal GenericMetadataSpecification(
        string description,
        Func<TModel, bool> predicate,
        TMetadata whenTrue,
        TMetadata whenFalse)
        : this(
            description,
            predicate,
            _ => whenTrue,
            _ => whenFalse)
    {
    }

    internal GenericMetadataSpecification(
        string description,
        Func<TModel, bool> predicate,
        Func<TModel, TMetadata> whenTrue,
        TMetadata whenFalse)
        : this(
            description,
            predicate,
            whenTrue,
            _ => whenFalse)
    {
    }

    internal GenericMetadataSpecification(
        string description,
        Func<TModel, bool> predicate,
        TMetadata whenTrue,
        Func<TModel, TMetadata> whenFalse)
        : this(
            description,
            predicate,
            _ => whenTrue,
            whenFalse)
    {
    }

    public override string Description => description.ThrowIfNullOrWhitespace(nameof(description));

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