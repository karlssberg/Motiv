﻿using Karlssberg.Motiv.HigherOrder;

namespace Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders.Explanation;

public readonly ref struct ExplanationWithDescriptionHigherOrderSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause,
    string candidateProposition,
    ReasonSource reasonSource)
{
    public SpecBase<IEnumerable<TModel>, string> CreateSpec() =>
        new HigherOrderSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            candidateProposition,
            reasonSource);

    public SpecBase<IEnumerable<TModel>, string> CreateSpec(string proposition) =>
        new HigherOrderSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)),
            reasonSource);
}