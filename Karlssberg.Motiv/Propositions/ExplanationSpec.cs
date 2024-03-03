namespace Karlssberg.Motiv.Propositions;

internal sealed class ExplanationSpec<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    string propositionalAssertion)
    : SpecBase<TModel, string>
{
    public ExplanationSpec(Func<TModel, bool> predicate, string propositionalAssertion) 
        : this(
            predicate, 
            _ => ReasonFromProposition(true, propositionalAssertion), 
            _ => ReasonFromProposition(false, propositionalAssertion), 
            propositionalAssertion)
    {
    }
    
    public override IProposition Proposition => new Proposition(propositionalAssertion);

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
            () => predicate(model),
            nameof(predicate));

    private string InvokeTrueBecauseFunction(TModel model) =>
        WrapException.CatchPredicateExceptionOnBehalfOfSpecType(
            this,
            () => trueBecause(model),
            nameof(trueBecause));

    private string InvokeFalseBecauseFunction(TModel model) =>
        WrapException.CatchPredicateExceptionOnBehalfOfSpecType(
            this,
            () => falseBecause(model),
            nameof(falseBecause));
    
    private static string ReasonFromProposition(bool isSatisfied, string proposition) =>
        (isSatisfied, proposition.Contains('!')) switch
        {
            (true, true) => $"({proposition})",
            (true, _)=> proposition,
            (false, true) when proposition.Contains('!') => $"!({proposition})",
            (false, _) => $"!{proposition}"
        };

}