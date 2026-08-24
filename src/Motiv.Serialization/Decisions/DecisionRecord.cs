namespace Motiv.Serialization;

/// <summary>
/// One evaluation of an audited rule, as it will be stored. The outcome payload already existed —
/// <see cref="RuleEvaluationResult{TMetadata}"/> is what <c>/api/checkout</c> has been building and
/// discarding — and everything around it is the envelope that makes the outcome evidence rather than
/// a response body.
/// </summary>
/// <remarks>
/// <para>
/// Reconstructing what a rule did needs <strong>three anchors, not one</strong>:
/// </para>
/// <list type="number">
/// <item>
/// <see cref="RuleVersion"/> — the rule's own composition, guaranteed to be a stored document because
/// <c>audited</c> lives on the document (see <c>RuleDocument.Audited</c>).
/// </item>
/// <item>
/// <see cref="BuildId"/> — the compiled specs the document references. A rule that resolves a name to
/// a C# delegate changes behaviour when the code is redeployed with no version bump, and a delegate
/// has nothing stable to fingerprint, so the build is recorded instead.
/// </item>
/// <item>
/// <see cref="ReferencedPropositionVersions"/> — what those names <em>meant</em> when the rule ran. A
/// rule version pins the rule's composition; it does not pin what <c>customer.is-active</c> said. That
/// is a fact about the evaluation rather than about the edit, which is why it belongs here and not in
/// the version log.
/// </item>
/// </list>
/// <para>
/// Together with <see cref="Input"/> — as far as the adopter's chosen capture posture preserved it —
/// the three anchors are what makes replay possible.
/// </para>
/// </remarks>
/// <param name="Id">This record's own identity.</param>
/// <param name="CorrelationId">
/// The decision this evaluation belonged to. Several rules evaluated inside one
/// <see cref="DecisionSnapshot"/> share one, because they were one decision.
/// </param>
/// <param name="TimestampUtc">When the evaluation completed.</param>
/// <param name="Caller">Who the decision was taken for, or null when nothing named them.</param>
/// <param name="RuleName">The rule that was evaluated.</param>
/// <param name="RuleVersion">The version of that rule's document — anchor 1.</param>
/// <param name="BuildId">The build that was live — anchor 2.</param>
/// <param name="ReferencedPropositionVersions">
/// Every authored proposition the rule resolved through, transitively, at the version it then had —
/// anchor 3.
/// </param>
/// <param name="Input">The captured input, or null when no capture posture applied.</param>
/// <param name="Outcome">The verdict and its full justification.</param>
public sealed record DecisionRecord(
    Guid Id,
    string CorrelationId,
    DateTimeOffset TimestampUtc,
    string? Caller,
    string RuleName,
    int RuleVersion,
    string BuildId,
    IReadOnlyList<PropositionVersion> ReferencedPropositionVersions,
    DecisionInput? Input,
    RuleEvaluationResult<object?> Outcome);

/// <summary>
/// One authored proposition at the version an evaluation resolved it at. A value, not a reference:
/// the anchor list is compared by adopters reconciling two records, and reference equality would make
/// every such comparison quietly false.
/// </summary>
/// <param name="Name">The proposition's name.</param>
/// <param name="Version">The version that was live when the rule was bound.</param>
public sealed record PropositionVersion(string Name, int Version);
