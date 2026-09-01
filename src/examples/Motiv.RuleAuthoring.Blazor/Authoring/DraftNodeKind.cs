namespace Motiv.RuleAuthoring.Blazor.Authoring;

/// <summary>What a <see cref="DraftNode" /> composes.</summary>
public enum DraftNodeKind
{
    /// <summary>A reference to a registered proposition.</summary>
    Spec,

    /// <summary>Negation of a single operand.</summary>
    Not,

    /// <summary>Conjunction, evaluating every operand.</summary>
    And,

    /// <summary>Disjunction, evaluating every operand.</summary>
    Or,

    /// <summary>Exclusive disjunction.</summary>
    XOr,

    /// <summary>Conjunction that short-circuits on the first unsatisfied operand.</summary>
    AndAlso,

    /// <summary>Disjunction that short-circuits on the first satisfied operand.</summary>
    OrElse
}
