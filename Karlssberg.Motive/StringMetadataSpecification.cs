namespace Karlssberg.Motive;

internal class StringMetadataSpecification<TModel>(
    string description,
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause) : SpecificationBase<TModel, string>
{
    private readonly Func<TModel, bool> _predicate = predicate.ThrowIfNull(nameof(predicate));
    private readonly Func<TModel, string> _trueBecause = trueBecause.ThrowIfNull(nameof(trueBecause));
    private readonly Func<TModel, string> _falseBecause = falseBecause.ThrowIfNull(nameof(falseBecause));

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
        trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause));
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
        trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause));
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
        falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause));
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
        trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
    }

    public override string Description => description;

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model) =>
        WrapException.IfIsSatisfiedByInvocationFails(this,
            () =>
            {
                var isSatisfied = InvokePredicate(model);
                var because = isSatisfied
                    ? InvokeTrueBecause(model)
                    : InvokeFalseBecause(model);

                return new BooleanResult<string>(isSatisfied, because, because);
            });

    private bool InvokePredicate(TModel model) =>
        WrapException.IfCallbackInvocationFails(
            this, 
            () => _predicate(model), 
            nameof(predicate));

    private string InvokeTrueBecause(TModel model) =>
        WrapException.IfCallbackInvocationFails(
            this, 
            () => _trueBecause(model), 
            nameof(trueBecause));

    private string InvokeFalseBecause(TModel model) =>
        WrapException.IfCallbackInvocationFails(
            this, 
            () => _falseBecause(model), 
            nameof(falseBecause));
}