namespace Karlssberg.Motiv;

public sealed class BooleanResultWithModel<TModel, TMetadata> : BooleanResultBase<TMetadata>
{
    internal BooleanResultWithModel(
        TModel model,
        BooleanResultBase<TMetadata> underlyingResult)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        UnderlyingResult = underlyingResult ?? throw new ArgumentNullException(nameof(underlyingResult));
    }

    public TModel Model { get; }

    public BooleanResultBase<TMetadata> UnderlyingResult { get; }

    public override bool IsSatisfied => UnderlyingResult.IsSatisfied;
    public override string Description => UnderlyingResult.Description;
    public override IEnumerable<string> Reasons => UnderlyingResult.Reasons;
}