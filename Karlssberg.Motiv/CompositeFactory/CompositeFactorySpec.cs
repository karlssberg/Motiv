namespace Karlssberg.Motiv.CompositeFactory;

internal sealed class CompositeFactorySpec<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> underlyingSpecFactory,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse,
    string propositionalAssertion)
    : SpecBase<TModel, TMetadata>
{
    /// <summary>Gets the description of the specification.</summary>
    public override IProposition Proposition => new Proposition(propositionalAssertion);

    /// <summary>Determines if the specification is satisfied by the given model.</summary>
    /// <param name="model">The model to be evaluated.</param>
    /// <returns>
    ///     A <see cref="BooleanResultBase{TMetadata}" /> indicating if the specification is satisfied and the resulting
    ///     metadata.
    /// </returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpecFactory(model).IsSatisfiedBy(model);
        
        var metadata = booleanResult.Satisfied switch
        {
            true => whenTrue(model),
            false => whenFalse(model),
        };

        return new CompositeFactoryBooleanResult<TMetadata, TUnderlyingMetadata>(booleanResult, metadata, Proposition);
    }
}