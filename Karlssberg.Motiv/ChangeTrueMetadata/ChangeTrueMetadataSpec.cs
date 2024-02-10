namespace Karlssberg.Motiv.ChangeTrueMetadata;

internal class ChangeTrueMetadataSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> underlyingSpec,
    Func<TModel, TMetadata> whenTrue)
    : SpecBase<TModel, TMetadata>
{
    public override string Description => underlyingSpec.Description;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpec.IsSatisfiedBy(model);
        return booleanResult.Satisfied switch
        {
            true => new ChangeTrueMetadataBooleanResult<TMetadata, TMetadata>(booleanResult, whenTrue(model)),
            false => booleanResult
        };
    }
}