namespace Karlssberg.Motiv.FirstOrder;

internal sealed class ExplanationSpec<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    string propositionalStatement)
    : SpecBase<TModel, string>
{
    internal ExplanationSpec(Func<TModel, bool> predicate, string propositionalStatement) 
        : this(
            predicate, 
            _ => ReasonFromProposition(true, propositionalStatement), 
            _ => ReasonFromProposition(false, propositionalStatement), 
            propositionalStatement)
    {
    }
    
    public override IProposition Proposition => new Proposition(propositionalStatement);

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model) =>
        WrapException.IfIsSatisfiedByMethodFails(
            this,
            () =>
            {
                var isSatisfied = InvokePredicate(model);
                
                var assertion = isSatisfied switch
                {  
                    true => InvokeTrueBecauseFunction(model),
                    false => InvokeFalseBecauseFunction(model)
                };

                return new MetadataBooleanResult<string>(
                    isSatisfied,
                    assertion,
                    assertion,
                    assertion);
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