namespace Motiv;
/// <summary>
/// Represents a "policy" whereby an arbitrary rule causes a single metadata instance to be returned for either the
/// true and false condition. The metadata is a string.
/// </summary>
/// <typeparam name="TModel">The model type that will be evaluated</typeparam>
public class Policy<TModel> : Policy<TModel, string>
{
    /// <inheritdoc/>
    public Policy(PolicyBase<TModel, string> spec) : base(spec)
    {
    }

    /// <inheritdoc/>
    public Policy(Func<PolicyBase<TModel, string>> specFactory) : base(specFactory)
    {
    }
}

/// <summary>
/// Represents a proposition that yields custom metadata based on the outcome of the underlying policy/predicate.
/// </summary>
/// <typeparam name="TModel">The model type that will be evaluated</typeparam>
/// <typeparam name="TMetadata">The metadata type</typeparam>
public class Policy<TModel, TMetadata> : PolicyBase<TModel, TMetadata>
{
    private readonly PolicyBase<TModel, TMetadata> _policy;


    /// <inheritdoc/>
    public Policy(PolicyBase<TModel, TMetadata> policy)
    {
        policy.ThrowIfNull(nameof(policy));

        _policy = policy;
        Description = policy.Description;
    }

    /// <inheritdoc/>
    public Policy(Func<PolicyBase<TModel, TMetadata>> policyFactory)
    {
        policyFactory.ThrowIfNull(nameof(policyFactory));

        _policy = policyFactory().ThrowIfFactoryOutputIsNull(nameof(policyFactory));
        Description = _policy.Description;
    }

    /// <inheritdoc/>
    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model) =>
        _policy.IsSatisfiedBy(model);

    /// <inheritdoc/>
    public override IEnumerable<SpecBase> Underlying => _policy.Underlying;

    /// <inheritdoc/>
    public override ISpecDescription Description { get; }
}
