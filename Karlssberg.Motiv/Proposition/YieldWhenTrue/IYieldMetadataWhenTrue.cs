using Karlssberg.Motiv.Proposition.YieldWhenFalse;

namespace Karlssberg.Motiv.Proposition.YieldWhenTrue;

public interface IYieldMetadataWhenTrue<TModel>
{
    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TAltMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="IYieldMetadataWhenFalse{TModel,TMetadata}" />.</returns>
    IYieldMetadataWhenFalse<TModel, TAltMetadata> YieldWhenTrue<TAltMetadata>(TAltMetadata whenTrue);


    /// <summary>Specifies the behavior when the condition is true.</summary>
    /// <typeparam name="TAltMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The function that defines the behavior when the condition is true.</param>
    /// <returns>An instance of <see cref="IYieldMetadataWhenFalse{TModel,TMetadata}" />.</returns>
    IYieldMetadataWhenFalse<TModel, TAltMetadata> YieldWhenTrue<TAltMetadata>(Func<TModel, TAltMetadata> whenTrue);
}