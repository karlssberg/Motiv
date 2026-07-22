namespace Motiv.Serialization;

/// <summary>
/// An async policy-flavoured rule: guarantees a single-value outcome, forwarding the
/// underlying policy's <see cref="ValueTask{TResult}"/> directly off an immutable snapshot.
/// Derives from <see cref="AsyncRule{TModel,TMetadata}"/> and shadows <see cref="EvaluateAsync"/>
/// with the policy result, exactly as <see cref="AsyncPolicyBase{TModel,TMetadata}"/> shadows
/// <see cref="AsyncSpecBase{TModel,TMetadata}"/>. Document updates must bind to an async policy
/// (<see cref="RuleErrorCode.PolicyRequired"/> otherwise).
/// </summary>
/// <typeparam name="TModel">The model type the rule evaluates against.</typeparam>
/// <typeparam name="TMetadata">The metadata type the rule yields.</typeparam>
public class AsyncPolicyRule<TModel, TMetadata> : AsyncRule<TModel, TMetadata>
{
    /// <summary>Creates an async policy rule whose default implementation is a compiled async policy.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultPolicy">The compiled default implementation.</param>
    /// <param name="description">An optional human-readable description.</param>
    public AsyncPolicyRule(string name, AsyncPolicyBase<TModel, TMetadata> defaultPolicy, string? description = null)
        : base(name, defaultPolicy ?? throw new ArgumentNullException(nameof(defaultPolicy)), description)
    {
    }

    /// <summary>Creates an async policy rule whose default implementation is a serialized rule document, bound at <see cref="RuleSet.Add"/>.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultDocument">The default rule-document JSON (e.g. from <see cref="RuleDocuments.Embedded(string)"/>).</param>
    /// <param name="description">An optional human-readable description.</param>
    public AsyncPolicyRule(string name, RuleDocumentSource defaultDocument, string? description = null)
        : base(name, defaultDocument, description)
    {
    }

    /// <inheritdoc />
    public override bool IsPolicy => true;

    /// <summary>Evaluates the current rule implementation, yielding the policy's single value.</summary>
    /// <remarks>Shadows the base method: an <see cref="AsyncRule{TModel,TMetadata}"/>-typed reference resolves to the base method and yields the spec-flavoured result.</remarks>
    /// <param name="model">The model to evaluate.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>The single-value policy result of the current implementation.</returns>
    public new ValueTask<PolicyResultBase<TMetadata>> EvaluateAsync(TModel model, CancellationToken cancellationToken = default) =>
        ((AsyncPolicyBase<TModel, TMetadata>)Snapshot().Spec).EvaluateAsync(model, cancellationToken);

    private protected override RuleError? RequirePolicy(AsyncSpecBase<TModel, TMetadata> spec) =>
        spec is AsyncPolicyBase<TModel, TMetadata>
            ? null
            : new RuleError("$.rule", RuleErrorCode.PolicyRequired,
                $"rule '{Name}' is a policy rule, but the document binds to a spec; " +
                "give the root a whenTrue/whenFalse decoration or reference a policy");
}
