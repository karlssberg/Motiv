namespace Motiv.Serialization;

/// <summary>
/// A node whose binding depends on propositions, and which therefore has to be rebound when one of
/// them is republished. Implemented by authored propositions and by document-backed rules.
/// </summary>
/// <remarks>
/// Rebinding is two-phase on purpose. Preparing every member of the closure before committing any of
/// them is what makes a publish all-or-nothing: a dependent that would stop binding is discovered
/// while the live state is still untouched.
/// </remarks>
internal interface IRebindable
{
    /// <summary>This node's identity in the dependency graph.</summary>
    NodeId Node { get; }

    /// <summary>
    /// Binds against the prospective source **without publishing**. Returns null and fills
    /// <paramref name="errors"/> when the node would no longer bind.
    /// </summary>
    IRebindCommit? PrepareRebind(ISpecSource prospective, List<RuleError> errors);
}

/// <summary>A prepared rebind, ready to be published.</summary>
internal interface IRebindCommit
{
    /// <summary>
    /// Publishes the prepared binding into the world being built. Must not fail, and is the whole of a
    /// rebind: there is no second, live-state half any more. That is what lets
    /// <see cref="BindingScope.PrepareClosure"/> apply a commit into a world it may still discard.
    /// </summary>
    void ApplyTo(ScopeGenerationBuilder builder);
}

/// <summary>
/// The commit for a node that had nothing to rebind — a rule on its compiled default, which
/// references nothing. Shared rather than duplicated per closed generic rule type, since it
/// carries no rule-specific state.
/// </summary>
internal sealed class NoRebindCommit : IRebindCommit
{
    public static NoRebindCommit Instance { get; } = new();

    public void ApplyTo(ScopeGenerationBuilder builder)
    {
    }
}
