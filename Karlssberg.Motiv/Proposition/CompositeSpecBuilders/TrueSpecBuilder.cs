using Karlssberg.Motiv.Proposition.HigherOrderSpecBuilders;
using Karlssberg.Motiv.Proposition.SpecBuilders.Metadata;
using Karlssberg.Motiv.Proposition.SpecBuilders.Reasons;

namespace Karlssberg.Motiv.Proposition.SpecBuilders;

public readonly struct TrueSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec)
{
    public FalseMetadataSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(TMetadata whenTrue) =>
        new(spec, _ => whenTrue);

    public FalseMetadataSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, TMetadata> whenTrue) =>
        new(spec, whenTrue);

    public FalseReasonsWithDescriptionSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(string trueBecause) =>
        new(spec, _ => trueBecause, trueBecause);

    public FalseReasonsSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(Func<TModel, string> trueBecause) =>
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

    public TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> As(
        Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, BooleanResult<TModel>> higherOrderPredicate) =>
        new(spec, results => higherOrderPredicate(results).Satisfied);
}