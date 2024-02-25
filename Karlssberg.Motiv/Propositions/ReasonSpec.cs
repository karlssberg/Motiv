namespace Karlssberg.Motiv.Propositions;

internal sealed class ReasonSpec<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    string propositionalStatement)
    : SpecBase<TModel, string>
{
    private readonly Func<TModel, string> _falseBecause = falseBecause.ThrowIfNull(nameof(falseBecause));
    private readonly Func<TModel, bool> _predicate = predicate.ThrowIfNull(nameof(predicate));
    private readonly Func<TModel, string> _trueBecause = trueBecause.ThrowIfNull(nameof(trueBecause));

    public override IProposition Proposition => new Proposition(propositionalStatement);

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model) =>
        WrapException.IfIsSatisfiedByMethodFails(
            this,
            () =>
            {
                var isSatisfied = InvokePredicate(model);
                var because = isSatisfied switch
                {  
                    true => InvokeTrueBecauseFunction(model),
                    false => InvokeFalseBecauseFunction(model)
                };

                return new BooleanResult<string>(isSatisfied, because, Proposition);
            });

    private bool InvokePredicate(TModel model) =>
        WrapException.CatchPredicateExceptionOnBehalfOfSpecType(
            this,
            () => _predicate(model),
            nameof(predicate));

    private string InvokeTrueBecauseFunction(TModel model) =>
        WrapException.CatchPredicateExceptionOnBehalfOfSpecType(
            this,
            () => _trueBecause(model),
            nameof(trueBecause));

    private string InvokeFalseBecauseFunction(TModel model) =>
        WrapException.CatchPredicateExceptionOnBehalfOfSpecType(
            this,
            () => _falseBecause(model),
            nameof(falseBecause));
}