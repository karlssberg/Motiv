namespace Motiv.Serialization;

/// <summary>What a proposed change targets.</summary>
public enum ChangeTargetKind
{
    /// <summary>A composed rule document.</summary>
    Rule,

    /// <summary>A single leaf proposition.</summary>
    Proposition
}

/// <summary>Identifies the artefact a proposed change applies to.</summary>
/// <param name="Kind">Whether the target is a rule or a proposition.</param>
/// <param name="Name">The dot-separated name.</param>
public sealed record ChangeTarget(ChangeTargetKind Kind, string Name)
{
    /// <summary>The parent namespace of the dotted name ("" when the name has no dot).</summary>
    public string Namespace => ComputeNamespace(Name);

    private static string ComputeNamespace(string name)
    {
        var lastDot = name.LastIndexOf('.');
        return lastDot < 0 ? string.Empty : name.Substring(0, lastDot);
    }
}

/// <summary>A single positive assent recorded against a <see cref="ChangeRequest"/>.</summary>
/// <param name="Approver">Identifies who gave the approval.</param>
/// <param name="TimestampUtc">When the approval was recorded.</param>
/// <param name="Roles">The roles the approver held at the time of approval.</param>
public sealed record Approval(string Approver, DateTimeOffset TimestampUtc, IReadOnlyList<string> Roles);

/// <summary>Workflow lifecycle — distinct from any version-row status (ticket 11 vs 13).</summary>
public enum ChangeRequestStatus
{
    /// <summary>Newly created; not yet submitted for approval.</summary>
    Draft,

    /// <summary>Submitted and accumulating approvals.</summary>
    InReview,

    /// <summary>Has met its approval requirement but has not yet been published.</summary>
    Approved,

    /// <summary>Published; terminal.</summary>
    Published,

    /// <summary>Rejected; terminal.</summary>
    Rejected,

    /// <summary>Withdrawn by its author; terminal.</summary>
    Withdrawn
}

/// <summary>
/// Derived from the diff except rollback, which is stored intent (a rollback and a
/// coincidentally-identical authoring share a diff but are different governance events).
/// </summary>
/// <param name="IsCreation">Whether the target did not previously exist.</param>
/// <param name="IsDeletion">Whether the change removes the target.</param>
/// <param name="IsMetadataOnly">Whether the change alters only non-behavioral metadata.</param>
/// <param name="TouchesAsyncSpec">Whether the change affects an asynchronously-evaluated specification.</param>
/// <param name="IsRollback">Whether this change is authored as a rollback of a prior version.</param>
/// <param name="RollbackOfVersion">The version being rolled back to, when <paramref name="IsRollback"/> is true.</param>
public sealed record ChangeClassification(
    bool IsCreation, bool IsDeletion, bool IsMetadataOnly, bool TouchesAsyncSpec,
    bool IsRollback, int? RollbackOfVersion);

/// <summary>One artefact's proposed new state. Null document = deletion / revert to compiled default.</summary>
/// <param name="Target">The artefact this change applies to.</param>
/// <param name="ProposedDocumentJson">The new document, or null to delete / revert to the compiled default.</param>
/// <param name="BaseVersion">The version this change was authored against.</param>
/// <param name="Classification">What kind of change this is, derived from the diff.</param>
/// <param name="ModelTypeId">
/// The registered model-type id a proposition *creation* is authored against. Null for rules, and
/// for edits to a proposition that already exists — an existing proposition's model type is fixed,
/// so restating it would only create a way for the two to disagree.
/// </param>
public sealed record ProposedChange(
    ChangeTarget Target, string? ProposedDocumentJson, int BaseVersion, ChangeClassification Classification,
    string? ModelTypeId = null);

/// <summary>The governance envelope: 1..N proposed changes that publish atomically.</summary>
public sealed class ChangeRequest
{
    private readonly List<Approval> _approvals = [];

    /// <summary>Creates a new change request in the <see cref="ChangeRequestStatus.Draft"/> state.</summary>
    /// <param name="id">The change request's identity.</param>
    /// <param name="author">Who authored the change request.</param>
    /// <param name="changeNote">A human-readable note describing the change. May be empty.</param>
    /// <param name="proposedChanges">The artefact changes this request publishes atomically.</param>
    /// <exception cref="ArgumentException"><paramref name="proposedChanges"/> is empty.</exception>
    public ChangeRequest(Guid id, string author, string changeNote, IReadOnlyList<ProposedChange> proposedChanges)
    {
        if (proposedChanges is null) throw new ArgumentNullException(nameof(proposedChanges));
        if (proposedChanges.Count == 0)
            throw new ArgumentException(
                "A change request must propose at least one change.", nameof(proposedChanges));

        Id = id;
        Author = author;
        ChangeNote = changeNote;
        ProposedChanges = new List<ProposedChange>(proposedChanges).AsReadOnly();
        Status = ChangeRequestStatus.Draft;
    }

    /// <summary>The change request's identity.</summary>
    public Guid Id { get; }

    /// <summary>Who authored the change request.</summary>
    public string Author { get; }

    /// <summary>A human-readable note describing the change.</summary>
    public string ChangeNote { get; }

    /// <summary>The artefact changes this request publishes atomically.</summary>
    public IReadOnlyList<ProposedChange> ProposedChanges { get; }

    /// <summary>The accumulating positive assents recorded against this request.</summary>
    public IReadOnlyList<Approval> Approvals => _approvals.AsReadOnly();

    /// <summary>The current workflow state.</summary>
    public ChangeRequestStatus Status { get; private set; }

    /// <summary>
    /// Why the request was rejected, when <see cref="Status"/> is
    /// <see cref="ChangeRequestStatus.Rejected"/>; null otherwise. A rejection is terminal-with-reason,
    /// not an approval row.
    /// </summary>
    public string? RejectionReason { get; private set; }

    /// <summary>Whether publication bypassed the ordinary approval requirement.</summary>
    public bool PublishedUnderBreakGlass { get; private set; }

    /// <summary>Records an approval, moving a draft request into review.</summary>
    /// <param name="approval">The approval to record.</param>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Status"/> is neither <see cref="ChangeRequestStatus.Draft"/> nor
    /// <see cref="ChangeRequestStatus.InReview"/>.
    /// </exception>
    internal void AddApproval(Approval approval)
    {
        if (Status is not (ChangeRequestStatus.Draft or ChangeRequestStatus.InReview))
            throw new InvalidOperationException(
                $"Cannot add an approval to a change request in the '{Status}' state.");

        _approvals.Add(approval);
        Status = ChangeRequestStatus.InReview;
    }

    /// <summary>Publishes the request, optionally bypassing the ordinary approval requirement.</summary>
    /// <param name="underBreakGlass">Whether this publication bypassed the ordinary approval requirement.</param>
    /// <exception cref="InvalidOperationException"><see cref="Status"/> is a terminal state.</exception>
    internal void MarkPublished(bool underBreakGlass)
    {
        if (Status is ChangeRequestStatus.Published or ChangeRequestStatus.Rejected or ChangeRequestStatus.Withdrawn)
            throw new InvalidOperationException(
                $"Cannot publish a change request in the '{Status}' state.");

        Status = ChangeRequestStatus.Published;
        PublishedUnderBreakGlass = underBreakGlass;
    }

    /// <summary>Rejects the request, terminating it with a reason.</summary>
    /// <param name="reason">Why the request was rejected.</param>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Status"/> is neither <see cref="ChangeRequestStatus.Draft"/> nor
    /// <see cref="ChangeRequestStatus.InReview"/>.
    /// </exception>
    internal void MarkRejected(string reason)
    {
        if (Status is not (ChangeRequestStatus.Draft or ChangeRequestStatus.InReview))
            throw new InvalidOperationException(
                $"Cannot reject a change request in the '{Status}' state.");

        Status = ChangeRequestStatus.Rejected;
        RejectionReason = reason;
    }

    /// <summary>Withdraws the request.</summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Status"/> is neither <see cref="ChangeRequestStatus.Draft"/> nor
    /// <see cref="ChangeRequestStatus.InReview"/>.
    /// </exception>
    internal void MarkWithdrawn()
    {
        if (Status is not (ChangeRequestStatus.Draft or ChangeRequestStatus.InReview))
            throw new InvalidOperationException(
                $"Cannot withdraw a change request in the '{Status}' state.");

        Status = ChangeRequestStatus.Withdrawn;
    }
}
