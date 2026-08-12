namespace Motiv.Serialization;

/// <summary>
/// Builds the synthetic <see cref="ChangeRequest"/> that <see cref="ApprovalGate.SetGate"/> uses as
/// its lockout pre-check: the friendliest possible change a candidate gate document could be asked
/// to approve.
/// </summary>
public static class SyntheticChangeRequests
{
    /// <summary>The number of distinct synthetic approvers the pre-check request carries.</summary>
    private const int ApproverCount = 100;

    /// <summary>
    /// Authors the synthetic request. Distinct from every synthetic approver, so
    /// <c>change.author-is-approver</c>-style clauses see it as peer-reviewed rather than
    /// self-approved.
    /// </summary>
    private const string Author = "synthetic-author";

    /// <summary>Where each synthetic approver's identifier is drawn from: <c>"{Prefix}{n}"</c> for n in 1..100.</summary>
    private const string ApproverPrefix = "synthetic-approver-";

    /// <summary>
    /// Every synthetic approval's timestamp, so the pre-check is deterministic. Built by hand rather
    /// than via <c>DateTimeOffset.UnixEpoch</c> — that static field is unavailable on
    /// <c>netstandard2.0</c>, one of this project's target frameworks.
    /// </summary>
    private static readonly DateTimeOffset ApprovalTimestamp = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The target every pre-check request proposes an edit to — the approval gate's own document,
    /// since that is what a candidate gate document is being asked to govern the replacement of.
    /// </summary>
    private const string TargetName = "motiv.governance.gate";

    /// <summary>
    /// A trivial, inert placeholder document. The pre-check evaluates a candidate gate's spec
    /// against the <see cref="ChangeRequest"/>'s shape (target, classification, approvals) — no
    /// built-in gate spec inspects <see cref="ProposedChange.ProposedDocumentJson"/> content, so its
    /// contents never influence the outcome; it exists only because <see cref="ProposedChange"/>
    /// requires a non-null document to represent an edit rather than a deletion.
    /// </summary>
    private const string PlaceholderDocumentJson = """{"rule": {"spec": "change.is-rollback"}}""";

    /// <summary>
    /// The most approvable gate-change imaginable: 100 distinct approvers each holding every
    /// known role, not self-approved, a plain single-rule edit under "motiv.governance". If even
    /// this is blocked, no real change could pass. Sound but incomplete — arbitrary predicates
    /// make satisfiability undecidable — so this is a footgun-catcher, not a proof.
    /// </summary>
    /// <param name="knownRoles">
    /// Every role known to the governance system. Each synthetic approval carries the full set, so
    /// a candidate gate document that requires any known role to have approved is still satisfied.
    /// </param>
    /// <returns>
    /// A change request in <see cref="ChangeRequestStatus.InReview"/>, with all 100 approvals
    /// recorded at the Unix epoch so the pre-check is deterministic — nothing about the outcome
    /// depends on wall-clock time.
    /// </returns>
    public static ChangeRequest MaximallyApprovable(IReadOnlyCollection<string> knownRoles)
    {
        if (knownRoles is null) throw new ArgumentNullException(nameof(knownRoles));

        var roles = knownRoles as IReadOnlyList<string> ?? [.. knownRoles];

        var proposedChange = new ProposedChange(
            new ChangeTarget(ChangeTargetKind.Rule, TargetName),
            PlaceholderDocumentJson,
            BaseVersion: 1,
            new ChangeClassification(
                IsCreation: false,
                IsDeletion: false,
                IsMetadataOnly: false,
                TouchesAsyncSpec: false,
                IsRollback: false,
                RollbackOfVersion: null));

        var changeRequest = new ChangeRequest(
            Guid.NewGuid(), Author, "synthetic lockout pre-check", [proposedChange]);

        for (var n = 1; n <= ApproverCount; n++)
            changeRequest.AddApproval(new Approval($"{ApproverPrefix}{n}", ApprovalTimestamp, roles));

        return changeRequest;
    }
}
