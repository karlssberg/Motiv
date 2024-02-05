using Karlssberg.Motiv.Proposition.YieldWhenFalse;

namespace Karlssberg.Motiv.Proposition;

public interface INestedMetadataSpecBuilderStart<TModel, TMetadata>
{
    SpecBase<TModel, TMetadata> CreateSpec(string description);
    IYieldMetadataWhenFalse<TModel, TMetadata> YieldWhenTrue(TMetadata whenTrue);
    IYieldMetadataWhenFalse<TModel, TMetadata> YieldWhenTrue(Func<TModel, TMetadata> whenTrue);
}