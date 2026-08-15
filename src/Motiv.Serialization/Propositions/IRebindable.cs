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
    /// Publishes the prepared binding into the world being built. Must not fail. Replaces the older
    /// pair of an overlay entry plus a <c>Commit()</c> that mutated live state: a commit now has one
    /// destination, and it is the world nobody is reading yet.
    /// </summary>
    void ApplyTo(ScopeGenerationBuilder builder);

    /// <summary>
    /// The remainder of the publish that <see cref="ApplyTo"/> cannot yet express, because it lands
    /// on a field the node still owns rather than in the generation. Transitional, and due to be
    /// deleted — read the remarks before doing so.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>There is exactly one caller</strong>, and it must be accounted for when this is
    /// removed: <see cref="ScopeGenerationBuilder.Apply"/>, which publishes a prepared closure.
    /// Nothing else may call it — in particular <see cref="BindingScope.PrepareClosure"/> must not,
    /// since it applies commits into a world that may yet be discarded, and a live write from there
    /// would publish a binding the caller went on to reject.
    /// </para>
    /// <para>
    /// <strong>It comes out in two halves.</strong> The authored half has landed:
    /// <c>AuthoredProposition.RebindCommit</c>'s implementation is now empty, because the authored
    /// proposition is immutable and <see cref="ApplyTo"/> already carries the replacement into the
    /// builder. What remains is <c>Rule</c>'s and <c>AsyncRule</c>'s, which still mutate live state
    /// here — that half goes when rule state moves into <see cref="RuleSlot"/>. This member — and
    /// <see cref="NoRebindCommit"/>'s empty body — can only be deleted once it has too. Apply calls it
    /// unconditionally rather than only for rule commits, so it never has to know which half of a
    /// mixed closure it is looking at.
    /// </para>
    /// </remarks>
    void Commit();
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

    public void Commit()
    {
    }
}
