namespace Motiv.BooleanPredicateProposition;

internal sealed class ExplanationProposition<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    ISpecDescription specDescription)
    : SpecBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => Enumerable.Empty<SpecBase>();
    
    internal ExplanationProposition(Func<TModel, bool> predicate, ISpecDescription specDescription) 
        : this(
            predicate, 
            _ => ReasonFromPropositionStatement(true, specDescription.Statement), 
            _ => ReasonFromPropositionStatement(false, specDescription.Statement), 
            specDescription)
    {
    }
    
    public override ISpecDescription Description => specDescription;

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var isSatisfied = InvokePredicate(model);

        var assertion = isSatisfied switch
        {
            true => InvokeTrueBecauseFunction(model),
            false => InvokeFalseBecauseFunction(model)
        };

        return new PropositionBooleanResult<string>(
            isSatisfied,
            new MetadataNode<string>(assertion, []),
            new Explanation(assertion, []),
            assertion);
    }

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
            (false, true) => $"!({proposition})",
            (false, _) => $"!{proposition}"
        };
}