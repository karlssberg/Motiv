﻿namespace Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders.Metadata;

public readonly ref struct MetadataCompositeFactorySpecFactory<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse)
{
    public SpecBase<TModel, TMetadata> CreateSpec(string proposition) =>
        new CompositeFactorySpec<TModel, TMetadata, TUnderlyingMetadata>(
            specPredicate,
            whenTrue,
            whenFalse,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}