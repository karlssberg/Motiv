namespace Karlssberg.Motive;

internal sealed class GenericMetadataSpecification<TModel, TMetadata>(
    string description,
    Func<TModel, bool> predicate,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse) : SpecificationBase<TModel, TMetadata>
{
    private readonly Func<TModel, bool> _predicate = Throw.IfNull(predicate, nameof(predicate));
    private readonly Func<TModel, TMetadata> _whenFalse = Throw.IfNull(whenFalse, nameof(whenFalse));

    private readonly Func<TModel, TMetadata> _whenTrue = Throw.IfNull(whenTrue, nameof(whenTrue));

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

    public override string Description => Throw.IfNullOrWhitespace(description, nameof(description));

    public override BooleanResultBase<TMetadata> Evaluate(TModel model) =>
        SpecificationException.WrapThrownExceptions(
            this,
            () =>
            {
                var isSatisfied = _predicate(model);
                var cause = isSatisfied
                    ? _whenTrue(model)
                    : _whenFalse(model);

                return new BooleanResult<TMetadata>(isSatisfied, cause, description);
            });
}