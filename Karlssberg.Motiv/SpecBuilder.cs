using Karlssberg.Motiv.Composite;
using Karlssberg.Motiv.Composite.CompositeSpecBuilders.Explanation;
using Karlssberg.Motiv.Composite.CompositeSpecBuilders.Metadata;
using Karlssberg.Motiv.HigherOrder;
using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv;

public readonly struct SpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec)
{
    public FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(TMetadata whenTrue) =>
        new(spec, _ => whenTrue);

    public FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, TMetadata> whenTrue) =>
        new(spec, whenTrue);

    public FalseAssertionWithDescriptionCompositeSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(string trueBecause) =>
        new(spec, _ => trueBecause, trueBecause);

    public FalseAssertionCompositeSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(Func<TModel, string> trueBecause) =>
        new(spec, trueBecause);
    
    public TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> As(
        Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate) =>
        new(spec, higherOrderPredicate);
    
    public TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsAllSatisfied() =>
        new(spec, r => r.AllTrue());
    
    public TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsAnySatisfied() =>
        new(spec, r => r.AnyTrue());
    
    public TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsNoneSatisfied() =>
        new(spec, r => r.AllFalse());
    
    public TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsNSatisfied(int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return new TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(spec, r => r.CountTrue() == n);
    }

    public TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsAtLeastNSatisfied(int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return new TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(spec, r => r.CountTrue() >= n);
    }

    public TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsAtMostNSatisfied(int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return new TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(spec, r => r.CountTrue() <= n);
    }
    
    public SpecBase<TModel, string> CreateSpec(string proposition) =>
        new CompositeSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            _ => proposition,
            _ => $"!{proposition}",
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}