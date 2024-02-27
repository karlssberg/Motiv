using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.Propositions;

internal sealed class CompositeFactorySpec<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> underlyingSpecFactory,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse,
    string propositionalAssertion)
    : SpecBase<TModel, TMetadata>
{
    private readonly SpecBase<TModel, TUnderlyingMetadata>? _underlyingSpec;
    
    internal CompositeFactorySpec(
        SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
        Func<TModel, TMetadata> whenTrue,
        Func<TModel, TMetadata> whenFalse,
        string propositionalAssertion) : this(_ => underlyingSpec, whenTrue, whenFalse, propositionalAssertion)
    {
        _underlyingSpec = underlyingSpec;
    }

    /// <summary>Gets the description of the specification.</summary>
    public override IProposition Proposition => new Proposition(propositionalAssertion, _underlyingSpec?.Proposition);

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

        return new ChangeMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(booleanResult, metadata, Proposition);
    }
}