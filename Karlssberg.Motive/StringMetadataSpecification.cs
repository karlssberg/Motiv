namespace Karlssberg.Motive;

internal class StringMetadataSpecification<TModel>(
    string description,
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause) : SpecificationBase<TModel, string>
{
    private readonly Func<TModel, string> _falseBecause = falseBecause ?? throw new ArgumentNullException(nameof(falseBecause));

    private readonly Func<TModel, bool> _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    private readonly Func<TModel, string> _trueBecause = trueBecause ?? throw new ArgumentNullException(nameof(trueBecause));

    internal StringMetadataSpecification(
        string description,
        Func<TModel, bool> predicate,
        string trueBecause,
        string falseBecause)
        : this(
            description,
            predicate,
            _ => trueBecause,
            _ => falseBecause)
    {
        Throw.IfNullOrWhitespace(trueBecause, nameof(trueBecause));
        Throw.IfNullOrWhitespace(falseBecause, nameof(falseBecause));
    }

    internal StringMetadataSpecification(
        Func<TModel, bool> predicate,
        string trueBecause,
        string falseBecause)
        : this(
            trueBecause,
            predicate,
            _ => trueBecause,
            _ => falseBecause)
    {
        Throw.IfNullOrWhitespace(trueBecause, nameof(trueBecause));
        Throw.IfNullOrWhitespace(falseBecause, nameof(falseBecause));
    }

    internal StringMetadataSpecification(
        string description,
        Func<TModel, bool> predicate,
        Func<TModel, string> trueBecause,
        string falseBecause)
        : this(
            description,
            predicate,
            trueBecause,
            _ => falseBecause)
    {
        Throw.IfNullOrWhitespace(falseBecause, nameof(falseBecause));
    }

    internal StringMetadataSpecification(
        Func<TModel, bool> predicate,
        string trueBecause,
        Func<TModel, string> falseBecause)
        : this(
            trueBecause,
            predicate,
            _ => trueBecause,
            falseBecause)
    {
        Throw.IfNullOrWhitespace(trueBecause, nameof(trueBecause));
    }

    public override string Description => description;

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model) =>
        SpecificationException.WrapThrownExceptions(
            this,
            () =>
            {
                _predicate(model);
                var isSatisfied = _predicate(model);
                var because = isSatisfied
                    ? _trueBecause(model)
                    : _falseBecause(model);

                return new BooleanResult<string>(isSatisfied, because, because);
            });
}