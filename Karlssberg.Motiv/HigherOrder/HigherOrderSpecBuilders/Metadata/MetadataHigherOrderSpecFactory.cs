﻿namespace Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders.Metadata;

public readonly ref struct MetadataHigherOrderSpecFactory<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector)
{
    public SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec(string proposition) =>
        new HigherOrderMultiMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            whenTrue,
            whenFalse,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)),
            causeSelector);
}