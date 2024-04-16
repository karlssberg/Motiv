namespace Karlssberg.Motiv.BooleanPredicateProposition;

internal sealed class ExplanationProposition<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    ISpecDescription specDescription)
    : SpecBase<TModel, string>
{
    internal ExplanationProposition(Func<TModel, bool> predicate, ISpecDescription specDescription) 
        : this(
            predicate, 
            _ => ReasonFromPropositionStatement(true, specDescription.Statement), 
            _ => ReasonFromPropositionStatement(false, specDescription.Statement), 
            specDescription)
    {
    }
    
    public override ISpecDescription Description => specDescription;

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
    
    private static string ReasonFromPropositionStatement(bool isSatisfied, string proposition) =>
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