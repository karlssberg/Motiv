using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// The change-request surface, and the seam through which every direct write reaches the same
/// approval gate. Mounted only when <see cref="MotivRulesBuilder.AddGovernance"/> registered a
/// <see cref="ChangeRequestSet"/>; without one the rule and proposition surfaces behave exactly as
/// they did before governance existed.
/// </summary>
internal static class MotivGovernanceEndpoints
{
    /// <summary>Maps the six change-request routes under the already-secured group.</summary>
    internal static void MapChangeRequestEndpoints(
        RouteGroupBuilder group, ChangeRequestSet changes, JsonSerializerOptions json)
    {
        group.MapGet("/change-requests", () =>
            Results.Json(changes.All.Select(ToResponse).ToArray(), json));

        group.MapGet("/change-requests/{id:guid}", (Guid id) =>
            changes.Find(id) is { } change
                ? Results.Json(ToResponse(change), json)
                : UnknownChangeRequest(json));

        group.MapPost("/change-requests", (ChangeRequestCreateRequest request, HttpContext http) =>
        {
            var proposed = request.Changes ?? [];

            // Authoring an envelope is authoring every target in it, so the weakest link decides.
            // Checked before the kinds are parsed: a caller with no grant learns nothing about
            // whether the rest of their request was well-formed.
            if (RefuseUnlessGrantedEverywhere(
                    http, json, GrantVerb.Author, proposed.Select(change => change.Name)) is { } refusal)
                return refusal;

            var authored = new List<NewProposedChange>(proposed.Count);
            foreach (var change in proposed)
            {
                if (ParseKind(change.Kind) is not { } kind)
                    return Results.Json(
                        new ErrorResponse(
                            $"Unknown change target kind '{change.Kind}'. Expected 'rule' or 'proposition'."),
                        json, statusCode: 400);

                authored.Add(new NewProposedChange(
                    kind, change.Name, DocumentJson(change.Document), change.BaseVersion,
                    change.RollbackOfVersion, change.ModelTypeId));
            }

            var result = changes.Create(
                PrincipalIdentity.Subject(http.User), request.ChangeNote ?? string.Empty, authored);

            return result is { Outcome: ChangeRequestOutcome.Ok, Change: { } created }
                ? Results.Json(ToResponse(created), json, statusCode: 201)
                : ToFailure(result, json, "created");
        });

        group.MapPost("/change-requests/{id:guid}/approvals", (Guid id, HttpContext http) =>
        {
            if (changes.Find(id) is not { } change)
                return UnknownChangeRequest(json);

            // Approving is folded into publishing: an approval is what lets the change land, so it
            // takes the same verb landing it does.
            if (RefuseUnlessGrantedEverywhere(http, json, GrantVerb.Publish, TargetNames(change)) is { } refusal)
                return refusal;

            var result = changes.Approve(
                id, PrincipalIdentity.Subject(http.User), PrincipalIdentity.Roles(http.User));

            return result is { Outcome: ChangeRequestOutcome.Ok, Change: { } approved }
                ? Results.Json(ToResponse(approved), json)
                : ToFailure(result, json, "approved");
        });

        group.MapPost("/change-requests/{id:guid}/rejection",
            (Guid id, ChangeRequestRejectionRequest request, HttpContext http) =>
        {
            if (changes.Find(id) is not { } change)
                return UnknownChangeRequest(json);

            if (RefuseUnlessGrantedEverywhere(http, json, GrantVerb.Publish, TargetNames(change)) is { } refusal)
                return refusal;

            var result = changes.Reject(id, request.Reason ?? string.Empty);
            return result is { Outcome: ChangeRequestOutcome.Ok, Change: { } rejected }
                ? Results.Json(ToResponse(rejected), json)
                : ToFailure(result, json, "rejected");
        });

        group.MapPost("/change-requests/{id:guid}/withdrawal", (Guid id, HttpContext http) =>
        {
            if (changes.Find(id) is null)
                return UnknownChangeRequest(json);

            // No grant check: withdrawal is the author retracting their own proposal, which the set
            // enforces as workflow state. A third party ending it is a rejection, which has its own
            // route and its own grant.
            var result = changes.Withdraw(id, PrincipalIdentity.Subject(http.User));
            return result is { Outcome: ChangeRequestOutcome.Ok, Change: { } withdrawn }
                ? Results.Json(ToResponse(withdrawn), json)
                : ToFailure(result, json, "withdrawn by this caller — only its author may withdraw it, and only while it is open");
        });

        group.MapPost("/change-requests/{id:guid}/publish", (Guid id, HttpContext http) =>
        {
            if (changes.Find(id) is not { } change)
                return UnknownChangeRequest(json);

            if (RefuseUnlessGrantedEverywhere(http, json, GrantVerb.Publish, TargetNames(change)) is { } refusal)
                return refusal;

            // Break-glass is Task 21's; until it lands nothing bypasses the gate.
            var result = changes.Publish(id, breakGlassActive: false);
            return result is { Outcome: ChangeRequestOutcome.Ok, Change: { } published }
                ? Results.Json(
                    new ChangeRequestPublishResponse(
                        ToResponse(published), result.PublishedVersions ?? new Dictionary<string, int>()),
                    json)
                : ToFailure(result, json, "published");
        });
    }

    /// <summary>
    /// Publishes one direct write — a rule PUT/DELETE, or a proposition create/update/delete — as a
    /// single-change request through the approval gate, so the ungoverned surface cannot be used to
    /// walk around the ceremony.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns null in the two cases where the caller should perform its own, ungoverned write:
    /// governance is not registered at all, or the governed publish refused for a reason that is not
    /// the gate. The second is deliberate. Nothing has been applied at that point, and a change
    /// request refuses in <see cref="ChangeRequestResult"/> terms — a flattened error list, a
    /// <c>ChangeTarget</c> — which are not the terms the rule and proposition surfaces have always
    /// answered in. Re-running the write reproduces the refusal callers already parse (a referenced
    /// proposition's referrer list, a name-taken 409, a cascade's broken dependents) byte for byte,
    /// and cannot bypass anything: the gate is what governs, and it already said yes.
    /// </para>
    /// <para>
    /// A gate refusal leaves the change request in Draft rather than discarding it, so the write the
    /// author attempted is already sitting there for a peer to approve.
    /// </para>
    /// </remarks>
    /// <param name="changes">The governance workflow, or null when governance is not registered.</param>
    /// <param name="http">The request, for the caller's identity.</param>
    /// <param name="json">The serializer options every response on this surface is written with.</param>
    /// <param name="kind">Whether the target is a rule or a proposition.</param>
    /// <param name="name">The target's dot-separated name.</param>
    /// <param name="documentJson">The proposed document, or null to withdraw / revert.</param>
    /// <param name="baseVersion">The version the caller authored against; 0 for a creation.</param>
    /// <param name="modelTypeId">The model-type id, when the write creates a proposition.</param>
    /// <param name="published">Builds the surface's own success response from the new version.</param>
    /// <returns>The response, or null when the caller should write directly.</returns>
    internal static IResult? PublishDirectWrite(
        ChangeRequestSet? changes,
        HttpContext http,
        JsonSerializerOptions json,
        ChangeTargetKind kind,
        string name,
        string? documentJson,
        int baseVersion,
        string? modelTypeId,
        Func<int, IResult> published)
    {
        if (changes is null)
            return null;

        var author = PrincipalIdentity.Subject(http.User);
        var created = changes.Create(
            author,
            $"direct {(documentJson is null ? "removal" : "write")} of {Label(kind)} '{name}' by {author}",
            [new NewProposedChange(kind, name, documentJson, baseVersion, null, modelTypeId)]);

        if (created is not { Outcome: ChangeRequestOutcome.Ok, Change: { } change })
            return null;

        var result = changes.Publish(change.Id, breakGlassActive: false);
        switch (result.Outcome)
        {
            case ChangeRequestOutcome.Ok:
                return published(
                    result.PublishedVersions is { } versions && versions.TryGetValue(name, out var version)
                        ? version
                        : 0);

            case ChangeRequestOutcome.GateBlocked:
                return Refused(result.Gate!, json);

            default:
                // Nothing was applied, and the record would otherwise sit in Draft forever pretending
                // to be a live proposal.
                changes.Reject(change.Id, $"the direct write was refused: {result.Outcome}");
                return null;
        }
    }

    /// <summary>The gate's refusal as a 403 — the one response governance adds to a direct write.</summary>
    private static IResult Refused(GateDecision decision, JsonSerializerOptions json) =>
        Results.Json(
            new GateRefusalResponse(decision.Reason, decision.Assertions, decision.Justification),
            json, statusCode: 403);

    /// <summary>Refuses unless the caller holds <paramref name="verb"/> on every one of the names.</summary>
    private static IResult? RefuseUnlessGrantedEverywhere(
        HttpContext http, JsonSerializerOptions json, GrantVerb verb, IEnumerable<string> names)
    {
        foreach (var name in names)
            if (GrantGate.Refuse(http, verb, name ?? string.Empty, json) is { } refusal)
                return refusal;
        return null;
    }

    private static IEnumerable<string> TargetNames(ChangeRequest change) =>
        change.ProposedChanges.Select(proposed => proposed.Target.Name);

    /// <summary>
    /// Parsed by hand rather than with <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/>,
    /// which would also accept the underlying numbers — a wire contract of "rule" or "proposition"
    /// should not quietly grow a second spelling.
    /// </summary>
    private static ChangeTargetKind? ParseKind(string? kind) => kind?.ToLowerInvariant() switch
    {
        "rule" => ChangeTargetKind.Rule,
        "proposition" => ChangeTargetKind.Proposition,
        _ => null
    };

    /// <summary>
    /// A supplied document as raw JSON. Both an absent property and an explicit <c>null</c> mean
    /// "no document", which is how a removal is expressed.
    /// </summary>
    private static string? DocumentJson(JsonElement document) =>
        document.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? null : document.GetRawText();

    private static string Label(ChangeTargetKind kind) => kind.ToString().ToLowerInvariant();

    private static ChangeRequestResponse ToResponse(ChangeRequest change) =>
        new(change.Id,
            change.Author,
            change.ChangeNote,
            change.Status.ToString(),
            change.RejectionReason,
            change.PublishedUnderBreakGlass,
            [.. change.ProposedChanges.Select(proposed => new ProposedChangeResponse(
                Label(proposed.Target.Kind),
                proposed.Target.Name,
                EndpointResponses.DocumentElement(proposed.ProposedDocumentJson),
                proposed.BaseVersion,
                proposed.Classification,
                proposed.ModelTypeId))],
            change.Approvals);

    /// <summary>
    /// Every non-Ok outcome as an HTTP answer. <paramref name="attempted"/> completes the sentence
    /// "the change request cannot be …", so an invalid-state refusal says which transition it refused.
    /// </summary>
    private static IResult ToFailure(
        ChangeRequestResult result, JsonSerializerOptions json, string attempted) =>
        result.Outcome switch
        {
            ChangeRequestOutcome.GateBlocked => Refused(result.Gate!, json),

            // NotFound from a publish is a *target* that does not exist — the change request itself
            // was found by the route handler before this ran.
            ChangeRequestOutcome.NotFound => Results.Json(
                new ChangeRequestErrorResponse(
                    result.FailedTarget is { } target
                        ? $"Unknown {Label(target.Kind)} '{target.Name}'."
                        : "Unknown change request.",
                    result.Errors, result.FailedTarget?.Name, null),
                json, statusCode: 404),

            ChangeRequestOutcome.VersionConflict => Results.Json(
                new ChangeRequestErrorResponse(
                    $"'{result.FailedTarget?.Name}' has moved on since this change was authored.",
                    result.Errors, result.FailedTarget?.Name, result.ConflictVersion),
                json, statusCode: 409),

            ChangeRequestOutcome.InvalidState => Results.Json(
                new ChangeRequestErrorResponse(
                    $"The change request cannot be {attempted}"
                    + (result.Change is { } change ? $" while it is '{change.Status}'." : "."),
                    result.Errors, result.FailedTarget?.Name, null),
                json, statusCode: 409),

            _ => Results.Json(
                new ChangeRequestErrorResponse(
                    "The change request is not publishable as authored.",
                    result.Errors, result.FailedTarget?.Name, null),
                json, statusCode: 400)
        };

    private static IResult UnknownChangeRequest(JsonSerializerOptions json) =>
        Results.Json(new ErrorResponse("Unknown change request."), json, statusCode: 404);
}
