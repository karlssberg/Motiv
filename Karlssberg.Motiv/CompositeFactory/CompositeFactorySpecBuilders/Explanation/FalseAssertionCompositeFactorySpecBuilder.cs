﻿namespace Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders.Explanation;

public readonly ref struct FalseAssertionCompositeFactorySpecBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> trueBecause)
{
    public ExplanationCompositeFactorySpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(specPredicate,
            trueBecause,
            (_ ,_) => falseBecause.ToEnumerable());

    public ExplanationCompositeFactorySpecFactory<TModel, TUnderlyingMetadata> WhenFalse(Func<TModel, string> falseBecause) =>
        new(specPredicate,
            trueBecause,
            (model, _) => falseBecause(model).ToEnumerable());
    
    public ExplanationCompositeFactorySpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause) =>
        new(specPredicate,
            trueBecause,
            (model, result) => falseBecause(model, result).ToEnumerable());
    
    public ExplanationCompositeFactorySpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(specPredicate,
            trueBecause,
            falseBecause);
}