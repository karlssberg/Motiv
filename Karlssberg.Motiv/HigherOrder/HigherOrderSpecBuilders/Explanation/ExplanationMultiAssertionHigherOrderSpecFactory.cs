﻿namespace Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders.Explanation;

public readonly ref struct ExplanationMultiAssertionHigherOrderSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector)
{
    public SpecBase<IEnumerable<TModel>, string> CreateSpec(string proposition) =>
        new HigherOrderMultiMetadataSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)),
            causeSelector);
}