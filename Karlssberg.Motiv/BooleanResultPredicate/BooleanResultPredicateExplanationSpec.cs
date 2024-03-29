﻿namespace Karlssberg.Motiv.BooleanResultPredicate;

internal sealed class BooleanResultPredicateExplanationSpec<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> predicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenFalse,
    string propositionalAssertion)
    : SpecBase<TModel, string>
{
    /// <summary>Gets the description of the specification.</summary>
    public override IProposition Proposition => new Proposition(propositionalAssertion);

    /// <summary>Determines if the specification is satisfied by the given model.</summary>
    /// <param name="model">The model to be evaluated.</param>
    /// <returns>
    ///     A <see cref="BooleanResultBase{TMetadata}" /> indicating if the specification is satisfied and the resulting
    ///     metadata.
    /// </returns>
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var booleanResult = predicate(model);
        
        var assertion = booleanResult.Satisfied switch
        {
            true => whenTrue(model, booleanResult),
            false => whenFalse(model, booleanResult),
        };

        return new BooleanResultPredicateBooleanResult<string, TUnderlyingMetadata>(
            booleanResult,
            assertion.ToEnumerable(),
            assertion.ToEnumerable(),
            Proposition.ToReason(booleanResult.Satisfied));
    }
}