using System.Text.Json;

namespace Motiv.Serialization.AspNetCore;

/// <summary>One artefact edit inside a change-request creation request.</summary>
/// <param name="Kind">Either <c>rule</c> or <c>proposition</c>, matched case-insensitively.</param>
/// <param name="Name">The dot-separated target name.</param>
/// <param name="Document">
/// The proposed document. Omitted (or JSON <c>null</c>) means "remove": withdraw the proposition, or
/// revert the rule to its default.
/// </param>
/// <param name="BaseVersion">
/// The version the edit was authored against — 0 when creating a proposition that does not exist
/// yet. A stale value is a 409 at publish time, not at creation.
/// </param>
/// <param name="RollbackOfVersion">The version this edit restores, when it is authored as a rollback.</param>
/// <param name="ModelTypeId">
/// A registered model-type id. Required when the edit creates a proposition; ignored otherwise.
/// </param>
public sealed record ProposedChangeRequest(
    string Kind,
    string Name,
    JsonElement Document,
    int BaseVersion,
    int? RollbackOfVersion,
    string? ModelTypeId);

/// <summary>A request to open a change request over one or more artefacts.</summary>
/// <param name="ChangeNote">A human-readable note describing the change.</param>
/// <param name="Changes">The edits that publish together. Must not be empty.</param>
public sealed record ChangeRequestCreateRequest(
    string ChangeNote, IReadOnlyList<ProposedChangeRequest> Changes);

/// <summary>A request to reject a change request.</summary>
/// <param name="Reason">Why the request is being rejected.</param>
public sealed record ChangeRequestRejectionRequest(string Reason);

/// <summary>One proposed change as the change-request surface reports it.</summary>
/// <param name="Kind">Either <c>rule</c> or <c>proposition</c>.</param>
/// <param name="Name">The dot-separated target name.</param>
/// <param name="Document">The proposed document, or null when the change removes the target.</param>
/// <param name="BaseVersion">The version the edit was authored against.</param>
/// <param name="Classification">What kind of change this is, as derived when it was authored.</param>
/// <param name="ModelTypeId">The model-type id a proposition creation was authored against, if any.</param>
public sealed record ProposedChangeResponse(
    string Kind,
    string Name,
    JsonElement? Document,
    int BaseVersion,
    ChangeClassification Classification,
    string? ModelTypeId);

/// <summary>A change request's full state.</summary>
/// <param name="Id">The change request's identity.</param>
/// <param name="Author">Who authored it.</param>
/// <param name="ChangeNote">The note describing the change.</param>
/// <param name="Status">The workflow state (Draft, InReview, Published, Rejected, Withdrawn).</param>
/// <param name="RejectionReason">Why it was rejected, when it was.</param>
/// <param name="PublishedUnderBreakGlass">Whether publication bypassed the approval gate.</param>
/// <param name="Changes">The edits that publish together.</param>
/// <param name="Approvals">The approvals recorded against it, with the roles held at the time.</param>
public sealed record ChangeRequestResponse(
    Guid Id,
    string Author,
    string ChangeNote,
    string Status,
    string? RejectionReason,
    bool PublishedUnderBreakGlass,
    IReadOnlyList<ProposedChangeResponse> Changes,
    IReadOnlyList<Approval> Approvals);

/// <summary>A successful publish.</summary>
/// <param name="Request">The change request, now in the Published state.</param>
/// <param name="PublishedVersions">
/// Each published target's new version, keyed by target name. A withdrawn proposition reports 0.
/// </param>
public sealed record ChangeRequestPublishResponse(
    ChangeRequestResponse Request, IReadOnlyDictionary<string, int> PublishedVersions);

/// <summary>
/// The approval gate's refusal, in the gate's own words. Returned with 403 from a publish the gate
/// blocked — and from a direct write, which publishes through the same gate.
/// </summary>
/// <param name="Reason">A one-line summary of why the gate refused.</param>
/// <param name="Assertions">Every contributing assertion string.</param>
/// <param name="Justification">The full hierarchical breakdown of the unmet conditions.</param>
public sealed record GateRefusalResponse(
    string Reason, IReadOnlyList<string> Assertions, string Justification);

/// <summary>A refused change-request operation.</summary>
/// <param name="Error">A human-readable description of the refusal.</param>
/// <param name="Errors">Why a proposed document was rejected; empty for other refusals.</param>
/// <param name="FailedTarget">The name of the target that failed, when one did.</param>
/// <param name="ConflictVersion">The target's current version, on a version conflict.</param>
public sealed record ChangeRequestErrorResponse(
    string Error,
    IReadOnlyList<RuleError> Errors,
    string? FailedTarget,
    int? ConflictVersion);
