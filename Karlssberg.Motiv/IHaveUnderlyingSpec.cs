namespace Karlssberg.Motiv;

public interface IHaveUnderlyingSpec<TModel, TUnderlyingMetadata>
{
    SpecBase<TModel, TUnderlyingMetadata> UnderlyingSpec { get; }
}