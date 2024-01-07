using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.Builder;

public class ChangeMetadataBuilder<TModel, TUnderlyingMetadata>(SpecificationBase<TModel, TUnderlyingMetadata> spec) :
    IRequireTrueReasonOrMetadata<TModel>,
    IRequireFalseReason<TModel>,
    IRequireActivation<TModel>
{
    private string? _candidateDescription;
    private Func<TModel, string>? _falseBecause;
    private Func<TModel, string>? _trueBecause;

    public IRequireFalseMetadata<TModel, TMetadata> YieldWhenTrue<TMetadata>(TMetadata whenTrue) =>
        new ChangeMetadataBuilder<TModel, TMetadata, TUnderlyingMetadata>(spec, _ => whenTrue);
    
    public IRequireFalseMetadata<TModel, TMetadata> YieldWhenTrue<TMetadata>(Func<TModel, TMetadata> whenTrue) =>
        new ChangeMetadataBuilder<TModel, TMetadata, TUnderlyingMetadata>(spec, whenTrue);
    
    public IRequireFalseMetadata<TModel, TMetadata> YieldWhenTrue<TMetadata>(Func<TMetadata> whenTrue) =>
        new ChangeMetadataBuilder<TModel, TMetadata, TUnderlyingMetadata>(spec, _ => whenTrue());

    public IRequireFalseReason<TModel> YieldWhenTrue(string trueBecause)
    {
        _candidateDescription = trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        trueBecause.ThrowIfNull(nameof(trueBecause));
        _trueBecause = _ => trueBecause;
        return this;
    }
    public IRequireFalseMetadata<TModel, string> YieldWhenTrue(Func<TModel, string> trueBecause) =>
        new ChangeMetadataBuilder<TModel, string, TUnderlyingMetadata>(spec, trueBecause);
    
    public IRequireFalseMetadata<TModel, string> YieldWhenTrue(Func<string> trueBecause) => 
        new ChangeMetadataBuilder<TModel, string, TUnderlyingMetadata>(spec, _ => trueBecause());

    public IRequireActivation<TModel> YieldWhenFalse(string falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        _falseBecause = _ => falseBecause;
        return this;
    }

    public IRequireActivation<TModel> YieldWhenFalse(Func<TModel, string> falseBecause)
    {
        _falseBecause = falseBecause.ThrowIfNull(nameof(falseBecause));
        return this;
    }

    public IRequireActivation<TModel> YieldWhenFalse(Func<string> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        _falseBecause = _ => falseBecause();
        return this;
    }
    public SpecificationBase<TModel, string> CreateSpec(string description)
    {
        return new ChangeMetadataTypeSpecification<TModel, string, TUnderlyingMetadata>(
            description.ThrowIfNullOrWhitespace(nameof(description)), 
            spec, 
            _trueBecause ?? throw new InvalidOperationException("Must specify a true metadata"),
            _falseBecause ?? throw new InvalidOperationException("Must specify a false metadata"));
    }
    public SpecificationBase<TModel, string> CreateSpec()
    {
        return new ChangeMetadataTypeSpecification<TModel, string, TUnderlyingMetadata>(
            _candidateDescription ?? spec.Description, 
            spec, 
            _trueBecause ?? throw new InvalidOperationException("Must specify a true metadata"),
            _falseBecause ?? throw new InvalidOperationException("Must specify a false metadata"));
    }
}

public class ChangeMetadataBuilder<TModel, TMetadata, TUnderlyingMetadata>(SpecificationBase<TModel, TUnderlyingMetadata> spec, Func<TModel, TMetadata> whenTrue) :
    IRequireFalseMetadata<TModel, TMetadata>,
    IRequireActivationWithDescription<TModel, TMetadata>
{
    private Func<TModel, TMetadata>? _whenFalse;
    
    public IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(TMetadata whenFalse)
    {
        _whenFalse = _ => whenFalse;
        return this;
    }
    
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
    
    public SpecificationBase<TModel, TMetadata> CreateSpec(string description) =>
        new ChangeMetadataTypeSpecification<TModel, TMetadata, TUnderlyingMetadata>(
            description.ThrowIfNullOrWhitespace(nameof(description)), 
            spec, 
            whenTrue,
            _whenFalse ?? throw new InvalidOperationException("Must specify a false metadata"));
}