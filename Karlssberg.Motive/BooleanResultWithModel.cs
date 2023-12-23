namespace Karlssberg.Motive;

public sealed record BooleanResultWithModel<TModel, TMetadata>(
    TModel Model,
    BooleanResultBase<TMetadata> UnderlyingResult) : BooleanResultBase<TMetadata>
{
    public TModel Model { get; } = Model ?? throw new ArgumentNullException(nameof(Model));

    public BooleanResultBase<TMetadata> UnderlyingResult { get; } = UnderlyingResult ?? throw new ArgumentNullException(nameof(UnderlyingResult));

    public override bool IsSatisfied => UnderlyingResult.IsSatisfied;
    public override string Description => UnderlyingResult.Description;
    public override string ToString() => Description;

    public override void Accept<TVisitor>(TVisitor visitor)
    {
        UnderlyingResult.Accept(visitor);
    }
}