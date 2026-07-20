namespace Motiv.Serialization;

/// <summary>
/// A serializable projection of a Motiv evaluation result, suitable for returning across an
/// HTTP boundary to a rules-engine frontend so it can render why a rule passed or failed.
/// </summary>
/// <typeparam name="TMetadata">The metadata type carried by the evaluated result.</typeparam>
public sealed class RuleEvaluationResult<TMetadata>
{
    /// <summary>Creates an evaluation-result projection.</summary>
    /// <param name="satisfied">Whether the rule was satisfied.</param>
    /// <param name="reason">The concise, operator-joined reason.</param>
    /// <param name="assertions">The flat, de-duplicated contributing assertions.</param>
    /// <param name="values">The metadata values that determined the outcome.</param>
    /// <param name="justification">The multi-line hierarchical justification string.</param>
    /// <param name="explanation">The de-noised causal explanation tree.</param>
    public RuleEvaluationResult(
        bool satisfied,
        string reason,
        IReadOnlyList<string> assertions,
        IReadOnlyList<TMetadata> values,
        string justification,
        ExplanationNode explanation)
    {
        Satisfied = satisfied;
        Reason = reason;
        Assertions = assertions;
        Values = values;
        Justification = justification;
        Explanation = explanation;
    }

    /// <summary>Whether the rule was satisfied.</summary>
    public bool Satisfied { get; }

    /// <summary>The concise, operator-joined reason for the outcome.</summary>
    public string Reason { get; }

    /// <summary>The flat, de-duplicated list of contributing assertions.</summary>
    public IReadOnlyList<string> Assertions { get; }

    /// <summary>The metadata values that determined the outcome.</summary>
    public IReadOnlyList<TMetadata> Values { get; }

    /// <summary>The multi-line hierarchical justification string.</summary>
    public string Justification { get; }

    /// <summary>The de-noised causal explanation tree.</summary>
    public ExplanationNode Explanation { get; }
}

/// <summary>A node in the de-noised causal explanation tree of an evaluation result.</summary>
public sealed class ExplanationNode
{
    /// <summary>Creates an explanation node.</summary>
    /// <param name="assertions">The assertions at this node.</param>
    /// <param name="underlying">The underlying (causal) explanation nodes.</param>
    public ExplanationNode(IReadOnlyList<string> assertions, IReadOnlyList<ExplanationNode> underlying)
    {
        Assertions = assertions;
        Underlying = underlying;
    }

    /// <summary>The assertions at this node.</summary>
    public IReadOnlyList<string> Assertions { get; }

    /// <summary>The underlying (causal) explanation nodes.</summary>
    public IReadOnlyList<ExplanationNode> Underlying { get; }
}
