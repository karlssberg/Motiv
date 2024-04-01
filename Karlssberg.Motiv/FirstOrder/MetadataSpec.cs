namespace Karlssberg.Motiv.FirstOrder;

/// <summary>
/// Represents a predicate that when evaluated returns a boolean result with associated metadata and description
/// of the underlying proposition that were responsible for the result.
/// </summary>
/// <typeparam name="TModel">The type of the input parameter.</typeparam>
/// <typeparam name="TMetadata">The type of the return value.</typeparam>
/// <returns>The return value.</returns>
internal sealed class MetadataSpec<TModel, TMetadata>(
    Func<TModel, bool> predicate,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse,
    string propositionalStatement)
    : SpecBase<TModel, TMetadata>
{
    /// <summary>Gets or sets the description of the proposition.</summary>
    public override IProposition Proposition => new Proposition(propositionalStatement);

    /// <summary>Determines if the specified model satisfies the proposition.</summary>
    /// <param name="model">The model to be evaluated.</param>
    /// <returns>A BooleanResultBase object containing the result of the evaluation.</returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) =>
        WrapException.IfIsSatisfiedByMethodFails(this,
            () =>
            {
                var isSatisfied = InvokePredicate(model);

                var metadata = isSatisfied switch
                {
                    true => InvokeWhenTrueFunction(model),
                    false => InvokeWhenFalseFunction(model)
                };
                
                var assertion = metadata switch
                {
                    string because => because,
                    _ => Proposition.ToReason(isSatisfied)
                };

                return new FirstOrderBooleanResult<TMetadata>(
                    isSatisfied,
                    metadata,
                    assertion,
                    Proposition.ToReason(isSatisfied));
            });

    private bool InvokePredicate(TModel model) =>
        WrapException.CatchPredicateExceptionOnBehalfOfSpecType(
            this,
            () => predicate(model),
            nameof(predicate));

    private TMetadata InvokeWhenTrueFunction(TModel model) =>
        WrapException.CatchPredicateExceptionOnBehalfOfSpecType(
            this,
            () => whenTrue(model),
            nameof(whenTrue));

    private TMetadata InvokeWhenFalseFunction(TModel model) =>
        WrapException.CatchPredicateExceptionOnBehalfOfSpecType(
            this,
            () => whenFalse(model),
            nameof(whenFalse));
}