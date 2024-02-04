namespace Karlssberg.Motiv.ChangeMetadata.YieldWhenFalse;

public interface IChangeMetadataBuilder<TModel, out TMetadata, TUnderlyingMetadata>
{
    internal SpecBase<TModel, TUnderlyingMetadata> Spec { get; }
    internal Func<TModel, TMetadata> WhenTrue { get; }
}
public interface IChangeReasonBuilder<TModel, TUnderlyingMetadata>
{
    internal SpecBase<TModel, TUnderlyingMetadata> Spec { get; }
    internal Func<TModel, string> TrueBecause { get; }
}
