using Motiv.BooleanPredicateProposition.PropositionBuilders;
using Motiv.BooleanResultPredicateProposition.PropositionBuilders;
using Motiv.SpecDecoratorProposition.PropositionBuilders;

namespace Motiv;

/// <summary>
/// Represents a proposition that yields custom metadata based on the outcome of the underlying spec/predicate.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class Spec<TModel, TMetadata> : SpecBase<TModel, TMetadata>
{
    private readonly SpecBase<TModel, TMetadata> _spec;

    /// <summary>
    /// Initializes a new instance of the Spec class with a SpecBase instance.
    /// </summary>
    /// <param name="spec">The base proposition associated with the Spec instance.</param>
    protected Spec(SpecBase<TModel, TMetadata> spec)
    {
        spec.ThrowIfNull(nameof(spec));

        _spec = spec;
        Description = spec.Description;
    }

    /// <summary>
    /// Initializes a new instance of the Spec class with a specification factory.
    /// </summary>
    /// <param name="specificationFactory">The specification factory to create the Spec instance.</param>
    protected Spec(Func<SpecBase<TModel, TMetadata>> specificationFactory)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));

        _spec = specificationFactory().ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        Description = _spec.Description;
    }

    /// <summary>
    /// Gets the underlying specifications that make up this composite proposition.
    /// </summary>
    public override IEnumerable<SpecBase> Underlying =>  _spec.Underlying;

    /// <summary>
    /// Gets the description of the proposition.
    /// </summary>
    public override ISpecDescription Description { get; }

    /// <summary>
    /// Determines whether the specified model satisfies the proposition.
    /// </summary>
    /// <param name="model">The model to be checked against the proposition.</param>
    /// <returns>A BooleanResultBase containing the result of the proposition check and the associated metadata.</returns>
    internal override BooleanResultBase<TMetadata> IsSatisfiedByInternal(TModel model) =>
        _spec.IsSatisfiedBy(model);
}

/// <summary>
/// Represents a proposition that defines a condition for a model of type TModel. This proposition is
/// associated with a string metadata.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public class Spec<TModel> : SpecBase<TModel, string>
{
    private readonly SpecBase<TModel, string> _spec;

    /// <summary>
    /// Initializes a new instance of the Spec class with a SpecBase instance.
    /// </summary>
    /// <param name="spec">The base proposition associated with the Spec instance.</param>
    protected Spec(SpecBase<TModel, string> spec)
    {
        spec.ThrowIfNull(nameof(spec));

        _spec = spec;
        Description = spec.Description;
    }

    /// <summary>
    /// Initializes a new instance of the Spec class with a specification factory.
    /// </summary>
    /// <param name="specFactory">The specification factory to create the Spec instance.</param>
    protected Spec(Func<SpecBase<TModel, string>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));

        _spec = specFactory().ThrowIfFactoryOutputIsNull(nameof(specFactory));
        Description = _spec.Description;
    }

    /// <summary>
    /// Gets the description of the proposition.
    /// </summary>
    public override ISpecDescription Description { get; }

    /// <summary>
    /// Gets the underlying specifications that makes up this composite proposition.
    /// </summary>
    public override IEnumerable<SpecBase> Underlying => Enumerable.Empty<SpecBase<TModel>>();

    /// <summary>
    /// Determines whether the specified model satisfies the proposition.
    /// </summary>
    /// <param name="model">The model to be checked against the proposition.</param>
    /// <returns>
    /// A BooleanResultBase containing the result of the proposition being applied to a moel and the associated metadata.
    /// </returns>
    internal override BooleanResultBase<string> IsSatisfiedByInternal(TModel model) => _spec.IsSatisfiedByInternal(model);
}

/// <summary>
/// Creates propositions using a fluent API.
/// </summary>
public static class Spec
{
    /// <summary>
    /// Commences the construction of a proposition using a predicate function.
    /// </summary>
    /// <param name="predicate">The predicate function to be used in the proposition.</param>
    /// <returns>A TrueFirstOrderSpecBuilder instance for further proposition building.</returns>
    public static BooleanPredicatePropositionBuilder<TModel> Build<TModel>(Func<TModel, bool> predicate)
    {
        predicate.ThrowIfNull(nameof(predicate));
        return new BooleanPredicatePropositionBuilder<TModel>(predicate);
    }

    /// <summary>
    /// Commences the construction of a proposition using a predicate function that returns a <see cref="BooleanResultBase{TMetadata}"/>.
    /// </summary>
    /// <param name="predicate">The predicate function to be used in the proposition.</param>
    /// <returns>A TrueFirstOrderSpecBuilder instance for further proposition building.</returns>
    public static BooleanResultPredicatePropositionBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        Func<TModel, BooleanResultBase<TMetadata>> predicate)
    {
        predicate.ThrowIfNull(nameof(predicate));
        return new BooleanResultPredicatePropositionBuilder<TModel, TMetadata>(predicate);
    }

    /// <summary>
    /// Commences the construction of a proposition using a predicate function that returns a <see cref="BooleanResultBase{TMetadata}"/>.
    /// </summary>
    /// <param name="predicate">The predicate function to be used in the proposition.</param>
    /// <returns>A TrueFirstOrderSpecBuilder instance for further proposition building.</returns>
    public static PolicyResultPredicatePropositionBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        Func<TModel, PolicyResultBase<TMetadata>> predicate)
    {
        predicate.ThrowIfNull(nameof(predicate));
        return new PolicyResultPredicatePropositionBuilder<TModel, TMetadata>(predicate);
    }

    /// <summary>
    /// Commences the construction of a specification using a specification factory function.
    /// </summary>
    /// <param name="factory">The specification factory function to be used in the specification.</param>
    /// <returns>A TrueCompositeFactorySpecBuilder instance for further specification building.</returns>
    public static TruePropositionBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        Func<SpecBase<TModel, TMetadata>> factory)
    {
        factory.ThrowIfNull(nameof(factory));
        return new TruePropositionBuilder<TModel, TMetadata>(factory());
    }

    /// <summary>
    /// Commences the construction of a specification using a specification factory function.
    /// </summary>
    /// <param name="policyFactory">The specification factory function to be used in the specification.</param>
    /// <returns>A TrueCompositeFactorySpecBuilder instance for further specification building.</returns>
    public static TruePolicyBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        Func<PolicyBase<TModel, TMetadata>> policyFactory)
    {
        policyFactory.ThrowIfNull(nameof(policyFactory));
        return new TruePolicyBuilder<TModel, TMetadata>(policyFactory());
    }

    /// <summary>
    /// Commences the construction of a specificaton that is derived from an existing specifcation.
    /// </summary>
    /// <param name="policy">The proposition upon which to derive a new proposition.</param>
    /// <returns>A TrueCompositeSpecBuilder instance for further specification building.</returns>
    public static TruePropositionBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        SpecBase<TModel, TMetadata> policy)
    {
        policy.ThrowIfNull(nameof(policy));
        return new TruePropositionBuilder<TModel, TMetadata>(policy);
    }

    /// <summary>
    /// Commences the construction of a specificaton that is derived from an existing specifcation.
    /// </summary>
    /// <param name="policy">The proposition upon which to derive a new proposition.</param>
    /// <returns>A TrueCompositeSpecBuilder instance for further specification building.</returns>
    public static TruePolicyBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        PolicyBase<TModel, TMetadata> policy)
    {
        policy.ThrowIfNull(nameof(policy));
        return new TruePolicyBuilder<TModel, TMetadata>(policy);
    }
}
