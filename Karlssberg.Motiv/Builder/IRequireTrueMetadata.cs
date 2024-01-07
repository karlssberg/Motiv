namespace Karlssberg.Motiv.Builder;

public interface IRequireTrueReason<TModel> 
{

    /// <summary>Sets the reason why the condition is true.</summary>
    /// <param name="trueBecause">The reason why the condition is true.</param>
    /// <returns>An instance of <see cref="IRequireFalseReason{TModel}" />.</returns>
    IRequireFalseReason<TModel> YieldWhenTrue(string trueBecause);
    
    /// <summary>Sets the reason why the condition is true.</summary>
    /// <param name="trueBecause">The reason why the condition is true.</param>
    /// <returns>An instance of <see cref="IRequireFalseReason{TModel}" />.</returns>
    IRequireFalseMetadata<TModel, string> YieldWhenTrue(Func<TModel, string> trueBecause);
    
    /// <summary>Sets the reason why the condition is true.</summary>
    /// <param name="trueBecause">The reason why the condition is true.</param>
    /// <returns>An instance of <see cref="IRequireFalseReason{TModel}" />.</returns>
    IRequireFalseMetadata<TModel, string> YieldWhenTrue(Func<string> trueBecause);
}

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
    IRequireFalseMetadata<TModel, TAltMetadata> YieldWhenTrue<TAltMetadata>(Func<TModel, TAltMetadata> whenTrue);
    
    /// <summary>Specifies the behavior when the condition is true.</summary>
    /// <typeparam name="TAltMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The function that defines the behavior when the condition is true.</param>
    /// <returns>An instance of <see cref="IRequireFalseMetadata{TModel,TMetadata}" />.</returns>
    IRequireFalseMetadata<TModel, TAltMetadata> YieldWhenTrue<TAltMetadata>(Func<TAltMetadata> whenTrue);
}

public interface IRequireTrueReasonOrMetadata<TModel> : IRequireTrueReason<TModel>, IRequireTrueMetadata<TModel>
{
}