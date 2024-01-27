namespace Karlssberg.Motiv.SpecBuilder.Phase3;

/// <summary>Represents an interface for building a specifcation.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata"></typeparam>
public interface ISpecFactory<TModel> : IDescriptiveSpecFactory<TModel, string>
{
    /// <summary>Provide a human readable explanation for when the condition is false.</summary>
    /// <param name="description">The description of the specification. If not specified, the description of the specification</param>
    /// <returns>A specification base.</returns>
    SpecBase<TModel, string> CreateSpec();
}