namespace Karlssberg.Motiv.Builder;

/// <summary>Represents a builder for creating specifications based on a predicate.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
internal class SpecBuilder<TModel>(Func<TModel, bool> predicate) :
    IRequireTrueReasonOrMetadata<TModel>,
    IRequireFalseReason<TModel>,
    IRequireFalseMetadata<TModel, string>,
    IRequireActivation<TModel>
{
    private string? _candidateDescription;
    private Func<TModel, string>? _falseBecause;
    private Func<TModel, string>? _trueBecause;

    /// <summary>Provide metadata for when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="whenTrue">The metadata associated with the behavior.</param>
    /// <returns>An instance of <see cref="IRequireFalseReason{TModel}" />.</returns>
    public IRequireFalseMetadata<TModel, TMetadata> YieldWhenTrue<TMetadata>(TMetadata whenTrue) =>
        new SpecBuilder<TModel, TMetadata>(predicate, _ => whenTrue);

    /// <summary>Provide a function that generates metadata for when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="whenTrue">The factory function to create the metadata.</param>
    /// <returns>An instance of <see cref="IRequireFalseReason{TModel}" />.</returns>
    public IRequireFalseMetadata<TModel, TMetadata> YieldWhenTrue<TMetadata>(Func<TModel, TMetadata> whenTrue) =>
        new SpecBuilder<TModel, TMetadata>(predicate, whenTrue);
    
    public IRequireFalseMetadata<TModel, TMetadata> YieldWhenTrue<TMetadata>(Func<TMetadata> whenTrue) => 
        new SpecBuilder<TModel, TMetadata>(predicate, _ => whenTrue());

    public IRequireFalseMetadata<TModel, string> YieldWhenTrue(Func<TModel, string> trueBecause)
    {
        _trueBecause = trueBecause.ThrowIfNull(nameof(trueBecause));
        return this;
    }

    public IRequireFalseMetadata<TModel, string> YieldWhenTrue(Func<string> trueBecause)
    {
        trueBecause.ThrowIfNull(nameof(trueBecause));
        _trueBecause = _ => trueBecause();
        return this;
    }

    /// <summary>
    /// Specifies the condition when the metadata is true.
    /// </summary>
    /// <param name="trueBecause">The reason why the metadata is true.</param>
    /// <returns>An instance of <see cref="IRequireFalseReason{TModel}"/> to continue building the specification.</returns>
    public IRequireFalseReason<TModel> YieldWhenTrue(string trueBecause)
    {
        _candidateDescription ??= trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        _trueBecause = _ => trueBecause;
        return this;
    }

    /// <summary>Specifies the reason when the metadata is false.</summary>
    /// <param name="whenFalse">The reason when the metadata is false.</param>
    /// <returns>An instance of <see cref="IRequireActivationWithDescription{TModel,TMetadata}" />.</returns>
    IRequireActivationWithDescription<TModel, string> IRequireFalseMetadata<TModel, string>.YieldWhenFalse(string whenFalse)
    {
        whenFalse.ThrowIfNullOrWhitespace(nameof(whenFalse));
        _falseBecause = _ => whenFalse;
        return this;
    }
    IRequireActivationWithDescription<TModel, string> IRequireFalseMetadata<TModel, string>.YieldWhenFalse(Func<TModel, string> whenFalse) => YieldWhenFalse(whenFalse);

    public IRequireActivation<TModel> YieldWhenFalse(string falseBecause)
    {
        falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause));
        _falseBecause = _ => falseBecause;
        return this;
    }

    /// <summary>Sets the function that provides the false because description for the specification.</summary>
    /// <typeparam name="TModel">The type of the model being validated.</typeparam>
    /// <param name="falseBecause">The function that provides the false because description.</param>
    /// <returns>The instance of the builder.</returns>
    public IRequireActivation<TModel> YieldWhenFalse(Func<TModel, string> falseBecause)
    {
        _falseBecause = falseBecause.ThrowIfNull(nameof(falseBecause));
        return this;
    }
    IRequireActivation<TModel> IRequireFalseReason<TModel>.YieldWhenFalse(Func<string> falseBecause) 
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        _falseBecause = _ => falseBecause();
        return this;
    }

    IRequireActivationWithDescription<TModel, string> IRequireFalseMetadata<TModel, string>.YieldWhenFalse(Func<string> falseBecause) 
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        _falseBecause = _ => falseBecause();
        return this;
    }
    
    public SpecificationBase<TModel, string> CreateSpec() => 
        new Spec<TModel, string>(
            _candidateDescription!,
            predicate,
            _trueBecause ?? throw new InvalidOperationException("Must specify a true metadata"),
            _falseBecause ?? throw new InvalidOperationException("Must specify a false metadata"));

    /// <summary>Sets the description for the specification.</summary>
    /// <param name="description">The description of the specification.</param>
    /// <returns>A new instance of the specification with the specified description.</returns>
    public SpecificationBase<TModel, string> CreateSpec(string description) =>
        new Spec<TModel, string>(
            description.ThrowIfNullOrWhitespace(nameof(description)),
            predicate,
            _trueBecause ?? throw new InvalidOperationException("Must specify a true metadata"),
            _falseBecause ?? throw new InvalidOperationException("Must specify a false metadata"));
}

/// <summary>Represents a builder for creating specifications with conditional metadata.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
internal class SpecBuilder<TModel, TMetadata>(Func<TModel, bool> predicate, Func<TModel, TMetadata> whenTrue) :
    IRequireFalseMetadata<TModel, TMetadata>,
    IRequireActivationWithDescription<TModel, TMetadata>
{
    private Func<TModel, TMetadata>? _whenFalse;
    
    /// <summary>Specifies the metadata to be used when the condition is false.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="whenFalse">The metadata to be used when the condition is false.</param>
    /// <returns>The instance of the builder.</returns>
    public IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(TMetadata whenFalse)
    {
        _whenFalse = _ => whenFalse;
        return this;
    }

    /// <summary>Specifies the function to be executed when the condition is false.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="whenFalse">The function to be executed when the condition is false.</param>
    /// <returns>The instance of the builder.</returns>
    public IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(Func<TModel, TMetadata> whenFalse)
    {
        _whenFalse = whenFalse;
        return this;
    }
    
    public IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(Func<TMetadata> whenFalse)
    {
        _whenFalse = _ => whenFalse();
        return this;
    }

    public SpecificationBase<TModel, TMetadata> CreateSpec(string description) => new Spec<TModel, TMetadata>(
        description.ThrowIfNullOrWhitespace(nameof(description)), 
        predicate,
        whenTrue, 
        _whenFalse ?? throw new InvalidOperationException("Must specify a false metadata"));
}