using Karlssberg.Motiv.Proposition.Factories;
using Karlssberg.Motiv.Proposition.YieldWhenFalse;

namespace Karlssberg.Motiv.Proposition;

internal class NestedFalseMetadataSpecBuilder<TModel, TMetadata>(
    Func<TModel, SpecBase<TModel, TMetadata>> specPredicate,
    Func<TModel, TMetadata> whenTrue) : 
    IDescriptiveSpecFactory<TModel, TMetadata>, 
    IYieldMetadataWhenFalse<TModel, TMetadata>
{
    private Func<TModel, TMetadata> _whenFalse = null!;


    public IDescriptiveSpecFactory<TModel, TMetadata> YieldWhenFalse(TMetadata whenFalse)
    {
        _whenFalse = _ => whenFalse;
        return this;
    }

    public IDescriptiveSpecFactory<TModel, TMetadata> YieldWhenFalse(Func<TModel, TMetadata> whenFalse)
    {
        _whenFalse = whenFalse;
        return this;
    }

    public SpecBase<TModel, TMetadata> CreateSpec(string description) =>
        new Spec<TModel, TMetadata>(
                description.ThrowIfNullOrWhitespace(nameof(description)),
                specPredicate)
            .YieldWhenTrue(whenTrue)
            .YieldWhenFalse(_whenFalse);
}