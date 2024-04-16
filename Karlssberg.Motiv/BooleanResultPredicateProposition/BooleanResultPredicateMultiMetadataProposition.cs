﻿namespace Karlssberg.Motiv.BooleanResultPredicateProposition;

public sealed class BooleanResultPredicateMultiMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> underlyingBooleanResultPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription)
    : SpecBase<TModel, TMetadata>
{
    /// <summary>Gets the name of the proposition.</summary>
    public override ISpecDescription Description => specDescription;

    /// <summary>Determines if the proposition is satisfied by the given model.</summary>
    /// <param name="model">The model to be evaluated.</param>
    /// <returns>
    ///     A <see cref="BooleanResultBase{TMetadata}" /> indicating if the proposition is satisfied and the resulting
    ///     metadata.
    /// </returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingBooleanResultPredicate(model);
        
        var metadata = new Lazy<TMetadata[]>(() => booleanResult.Satisfied switch
        {
            true => whenTrue(model, booleanResult).ToArray(),
            false => whenFalse(model, booleanResult).ToArray()
        });

        var assertions = new Lazy<string[]>(() => metadata.Value switch
        {
            IEnumerable<string> assertion => assertion.ToArray(),
            _ => [Description.ToReason(booleanResult.Satisfied)]
        });

        return new BooleanResultWithUnderlying<TMetadata,TUnderlyingMetadata>(
            booleanResult,
            MetadataTree,
            Explanation,
            Description.ToReason(booleanResult.Satisfied));

        Explanation Explanation() => new(assertions.Value)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };

        MetadataTree<TMetadata> MetadataTree() => 
            new(metadata.Value, 
                booleanResult.ResolveMetadataTrees<TMetadata, TUnderlyingMetadata>());
    }
}

