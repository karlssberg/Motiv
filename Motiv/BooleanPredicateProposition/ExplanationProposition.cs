namespace Motiv.BooleanPredicateProposition;

internal sealed class ExplanationProposition<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    ISpecDescription specDescription)
    : PolicyBase<TModel, string>
{

    public override IEnumerable<SpecBase> Underlying => [];

    internal ExplanationProposition(Func<TModel, bool> predicate, ISpecDescription specDescription)
        : this(
            predicate,
            _ => ReasonFromPropositionStatement(true, specDescription.Statement),
            _ => ReasonFromPropositionStatement(false, specDescription.Statement),
            specDescription)
    {
    }


    public override ISpecDescription Description => specDescription;

    public override PolicyResultBase<string> Execute(TModel model)
    {
        var isSatisfied = InvokePredicate(model);

        var assertion = GetAssertion(model, isSatisfied);

        return CreatePolicyResult(isSatisfied, assertion);
    }

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model) => Execute(model);

    private Lazy<string> GetAssertion(TModel model, bool isSatisfied) =>
        new(() =>
            isSatisfied switch
            {
                true => InvokeTrueBecauseFunction(model),
                false => InvokeFalseBecauseFunction(model)
            });

    private static PolicyResultBase<string> CreatePolicyResult(bool isSatisfied, Lazy<string> assertion) =>
        new PropositionPolicyResult<string>(
            isSatisfied,
            assertion,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(assertion.Value, [])),
            new Lazy<Explanation>(() => new Explanation(assertion.Value, [])),
            assertion);

    private bool InvokePredicate(TModel model) =>
        WrapException.CatchFuncExceptionOnBehalfOfSpecType(
            this,
            () => predicate(model),
            nameof(predicate));

    private string InvokeTrueBecauseFunction(TModel model) =>
        WrapException.CatchFuncExceptionOnBehalfOfSpecType(
            this,
            () => trueBecause(model),
            nameof(trueBecause));

    private string InvokeFalseBecauseFunction(TModel model) =>
        WrapException.CatchFuncExceptionOnBehalfOfSpecType(
            this,
            () => falseBecause(model),
            nameof(falseBecause));

    private static string ReasonFromPropositionStatement(bool isSatisfied, string proposition) =>
        (isSatisfied, proposition.ContainsReservedCharacters()) switch
        {
            (true, true) => $"({proposition})",
            (true, _)=> proposition,
            (false, true) => $"¬({proposition})",
            (false, _) => $"¬{proposition}"
        };
}
