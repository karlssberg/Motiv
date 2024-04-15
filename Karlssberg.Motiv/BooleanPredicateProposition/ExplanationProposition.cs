namespace Karlssberg.Motiv.BooleanPredicateProposition;

internal sealed class ExplanationProposition<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    string propositionalStatement)
    : SpecBase<TModel, string>
{
    internal ExplanationProposition(Func<TModel, bool> predicate, string propositionalStatement) 
        : this(
            predicate, 
            _ => ReasonFromProposition(true, propositionalStatement), 
            _ => ReasonFromProposition(false, propositionalStatement), 
            propositionalStatement)
    {
    }
    
    public override ISpecDescription Description => new SpecDescription(propositionalStatement);

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

                return new PropositionBooleanResult<string>(
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
        (isSatisfied, ContainsEscapableCharacters(proposition)) switch
        {
            (true, true) => $"({proposition})",
            (true, _)=> proposition,
            (false, true) => $"!({proposition})",
            (false, _) => $"!{proposition}"
        };

    private static bool ContainsEscapableCharacters(string proposition) => 
        proposition.Contains('!') || proposition.Contains('(') || proposition.Contains(')');
}