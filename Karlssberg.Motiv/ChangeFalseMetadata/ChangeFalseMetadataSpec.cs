
namespace Karlssberg.Motiv.ChangeFalseMetadata;

public class ChangeFalseMetadataSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> underlyingSpec,
    Func<TModel, TMetadata> whenFalse)
    : SpecBase<TModel, TMetadata>
{
    public override string Description => underlyingSpec.Description;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpec.IsSatisfiedBy(model);
        return booleanResult.Value switch
        {
            true => booleanResult,
            false => new ChangeTrueMetadataBooleanResult<TMetadata, TMetadata>(booleanResult, whenFalse(model))
        };
    }
}