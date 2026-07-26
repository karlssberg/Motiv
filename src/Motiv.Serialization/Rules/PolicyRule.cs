namespace Motiv.Serialization;

/// <summary>
/// A policy-flavoured rule: guarantees a single-value outcome. Derives from
/// <see cref="Rule{TModel,TMetadata}"/> and shadows <see cref="Evaluate"/> with the policy
/// result, exactly as <see cref="PolicyBase{TModel,TMetadata}"/> shadows
/// <see cref="SpecBase{TModel,TMetadata}"/>. Document updates must bind to a policy
/// (<see cref="RuleErrorCode.PolicyRequired"/> otherwise).
/// </summary>
/// <typeparam name="TModel">The model type the rule evaluates against.</typeparam>
/// <typeparam name="TMetadata">The metadata type the rule yields.</typeparam>
public class PolicyRule<TModel, TMetadata> : Rule<TModel, TMetadata>
{
    /// <summary>Creates a policy rule whose default implementation is a compiled policy.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultPolicy">The compiled default implementation.</param>
    /// <param name="description">An optional human-readable description.</param>
    public PolicyRule(string name, PolicyBase<TModel, TMetadata> defaultPolicy, string? description = null)
        : base(name, defaultPolicy ?? throw new ArgumentNullException(nameof(defaultPolicy)), description)
    {
    }

    /// <summary>Creates a policy rule whose default implementation is a serialized rule document, bound at <see cref="RuleSet.Add"/>.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultDocument">The default rule-document JSON (e.g. from <see cref="RuleDocuments.Embedded(string)"/>).</param>
    /// <param name="description">An optional human-readable description.</param>
    public PolicyRule(string name, RuleDocumentSource defaultDocument, string? description = null)
        : base(name, defaultDocument, description)
    {
    }

    /// <inheritdoc />
    public override bool IsPolicy => true;

    /// <summary>Evaluates the current rule implementation, yielding the policy's single value.</summary>
    /// <remarks>Shadows the base method: a <see cref="Rule{TModel,TMetadata}"/>-typed reference resolves to the base method and yields the spec-flavoured result.</remarks>
    /// <param name="model">The model to evaluate.</param>
    /// <returns>The single-value policy result of the current implementation.</returns>
    public new PolicyResultBase<TMetadata> Evaluate(TModel model) =>
        ((PolicyBase<TModel, TMetadata>)Snapshot().Spec).Evaluate(model);

    private protected override RuleError? RequirePolicy(SpecBase<TModel, TMetadata> spec) =>
        spec is PolicyBase<TModel, TMetadata>
            ? null
            : new RuleError("$.rule", RuleErrorCode.PolicyRequired,
                $"rule '{Name}' is a policy rule, but the document binds to a spec; " +
                "give the root a whenTrue/whenFalse decoration or reference a policy");
}
