using Karlssberg.Motiv.SpecBuilder.Phase2;

namespace Karlssberg.Motiv.SpecBuilder.Phase1;

public interface IRequireTrueMetadata<TModel>
{
    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TAltMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="IRequireFalseMetadata{TModel,TMetadata}" />.</returns>
    IRequireFalseMetadata<TModel, TAltMetadata> YieldWhenTrue<TAltMetadata>(TAltMetadata whenTrue);

    /// <summary>Specifies the behavior when the condition is true.</summary>
    /// <typeparam name="TAltMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The function that defines the behavior when the condition is true.</param>
    /// <returns>An instance of <see cref="IRequireFalseMetadata{TModel,TMetadata}" />.</returns>
    IRequireFalseMetadata<TModel, TAltMetadata> YieldWhenTrue<TAltMetadata>(Func<TAltMetadata> whenTrue);

    /// <summary>Specifies the behavior when the condition is true.</summary>
    /// <typeparam name="TAltMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The function that defines the behavior when the condition is true.</param>
    /// <returns>An instance of <see cref="IRequireFalseMetadata{TModel,TMetadata}" />.</returns>
    IRequireFalseMetadata<TModel, TAltMetadata> YieldWhenTrue<TAltMetadata>(Func<TModel, TAltMetadata> whenTrue);
}