using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

            // A blank name can be neither granted against nor resolved to a target, so it is
            // refused before either — ahead of the grant check below, and well before Create()
            // would otherwise hand it to a lookup that assumes a real name.
            for (var i = 0; i < proposed.Count; i++)
                if (string.IsNullOrWhiteSpace(proposed[i].Name))
                    return Results.Json(
                        new ErrorResponse($"Change {i} must name a target; a blank name is not valid."),
                        json, statusCode: 400);

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

                // Removal is spelled `"document": null`, never by leaving the property out. A
                // forgotten field and a deliberate deletion are the same bytes on the wire otherwise,
                // and the deletion is the destructive one — so the ambiguity is refused rather than
                // resolved in its favour.
                if (change.Document.ValueKind == JsonValueKind.Undefined)
                    return Results.Json(
                        new ErrorResponse(
                            $"The change to {Label(kind)} '{change.Name}' must include a document. "
                            + "Pass \"document\": null to remove the target."),
                        json, statusCode: 400);

                authored.Add(new NewProposedChange(
                    kind, change.Name, DocumentJson(change.Document), change.BaseVersion,
                    change.RollbackOfVersion, change.ModelTypeId, change.Description));
            }

            var result = changes.Create(
                PrincipalIdentity.Subject(http.User), request.ChangeNote ?? string.Empty, authored);

            // Create only ever answers Ok or Invalid — never GateBlocked, NotFound, VersionConflict
            // or InvalidState — so ToFailure's invalidState message (which only the InvalidState arm
            // reads) never surfaces here; the switch's own default text answers an Invalid outcome.
            return result is { Outcome: ChangeRequestOutcome.Ok, Change: { } created }
                ? Results.Json(ToResponse(created), json, statusCode: 201)
                : ToFailure(result, json);
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
                : ToFailure(result, json, "The change request cannot be approved once it is closed.");
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
                : ToFailure(result, json, "The change request cannot be rejected once it is closed.");
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
                : ToFailure(result, json,
                    "Only the change request's author may withdraw it, and only while it is open.");
        });

        group.MapPost("/change-requests/{id:guid}/publish", (Guid id, HttpContext http) =>
        {
            if (changes.Find(id) is not { } change)
                return UnknownChangeRequest(json);

            if (RefuseUnlessGrantedEverywhere(http, json, GrantVerb.Publish, TargetNames(change)) is { } refusal)
                return refusal;

            var breakGlassActive = ResolveBreakGlass(http).Active(DateTimeOffset.UtcNow);
            var result = changes.Publish(id, breakGlassActive);

            if (result is not { Outcome: ChangeRequestOutcome.Ok, Change: { } published })
                return ToFailure(result, json, "The change request cannot be published once it is closed.");

            // The durable stamp lives on the change request itself (PublishedUnderBreakGlass, already
            // in the response below); this is the loud, out-of-band echo of it.
            if (published.PublishedUnderBreakGlass)
                AuditLog(http).LogWarning(
                    "MOTIV-AUDIT break-glass publish: change request {ChangeRequestId} by {Author} " +
                    "published with the approval gate DISABLED.",
                    published.Id, ForLog(PrincipalIdentity.Subject(http.User)));

            return Results.Json(
                new ChangeRequestPublishResponse(
                    ToResponse(published), result.PublishedVersions ?? new Dictionary<string, int>()),
                json);
        });
    }

    /// <summary>
    /// Maps <c>GET /gate</c>, <c>PUT /gate</c>, and <c>DELETE /gate</c>: inspecting, replacing, and
    /// resetting the active approval-gate document.
    /// </summary>
    /// <remarks>
    /// The gate never governs itself. Reconfiguring it is an <c>administer</c> act, checked by
    /// <see cref="GrantGate.RefuseUnlessAdministrator"/> at the endpoint boundary — never a
    /// <see cref="ChangeRequest"/> routed through the very gate being reconfigured. A gate that had
    /// to approve its own replacement could not be locked down further than its current document
    /// already allows, which defeats the purpose of tightening it.
    /// </remarks>
    /// <param name="group">The already-secured route group to map onto.</param>
    /// <param name="gate">The approval gate these routes inspect and reconfigure.</param>
    /// <param name="json">The serializer options every response on this surface is written with.</param>
    internal static void MapGateEndpoints(RouteGroupBuilder group, ApprovalGate gate, JsonSerializerOptions json)
    {
        group.MapGet("/gate", () => Results.Json(ToGetResponse(gate.DocumentJson), json));

        group.MapPut("/gate", (GatePutRequest request, HttpContext http) =>
        {
            if (GrantGate.RefuseUnlessAdministrator(http, json) is { } refusal)
                return refusal;

            if (request.Document.ValueKind == JsonValueKind.Undefined)
                return EndpointResponses.MissingDocument(json);

            var result = gate.SetGate(request.Document.GetRawText(), KnownRoles(http));

            return result.Outcome switch
            {
                GateUpdateOutcome.Updated => Results.Json(ToGetResponse(gate.DocumentJson), json),
                GateUpdateOutcome.Invalid =>
                    Results.Json(new ValidationResponse(result.Errors), json, statusCode: 400),

                GateUpdateOutcome.WouldLockOut => Results.Json(
                    new GateRefusalResponse(
                        result.PreCheck!.Reason, result.PreCheck.Assertions, result.PreCheck.Justification),
                    json, statusCode: 422),

                _ => throw new UnreachableException($"Unhandled {nameof(GateUpdateOutcome)}: {result.Outcome}.")
            };
        });

        group.MapDelete("/gate", (HttpContext http) =>
        {
            if (GrantGate.RefuseUnlessAdministrator(http, json) is { } refusal)
                return refusal;

            gate.SetGate(null, KnownRoles(http));
            return Results.NoContent();
        });
    }

    private static GateGetResponse ToGetResponse(string? documentJson) =>
        new(EndpointResponses.DocumentElement(documentJson), documentJson is null);

    /// <summary>
    /// The role universe <see cref="ApprovalGate.SetGate"/> reserves for its lockout pre-check, or
    /// empty when no <see cref="IGrantSource"/> is registered — grants (and the roles they know
    /// about) are opt-in, same as everywhere else on this surface.
    /// </summary>
    private static IReadOnlyCollection<string> KnownRoles(HttpContext http) =>
        http.RequestServices.GetService<IGrantSource>()?.KnownRoles ?? [];

    /// <summary>
    /// Runs one direct rule write — a PUT or a DELETE — through the approval gate, and, when the
    /// gate allows it, through the very same core the ungoverned endpoint calls.
    /// </summary>
    /// <remarks>
    /// The write executes exactly once and reports in <see cref="RuleUpdateResult"/> terms, so
    /// <paramref name="written"/> can map it with the mapping this surface has always used. Nothing
    /// is recorded: a direct write is not a proposal, and a caller the gate refuses is pointed at
    /// the change-request surface, where proposals live.
    /// </remarks>
    /// <param name="governance">The governance workflow.</param>
    /// <param name="http">The request, for the caller's identity.</param>
    /// <param name="json">The serializer options every response on this surface is written with.</param>
    /// <param name="operation">Which ungoverned write this stands in for.</param>
    /// <param name="name">The rule's name.</param>
    /// <param name="documentJson">The proposed document, or null to revert to the default.</param>
    /// <param name="baseVersion">The version the caller authored against.</param>
    /// <param name="written">Maps the write's own outcome onto this surface's response.</param>
    /// <returns>The gate's 403 refusal, or whatever <paramref name="written"/> produced.</returns>
    internal static IResult GovernedRuleWrite(
        ChangeRequestSet governance,
        HttpContext http,
        JsonSerializerOptions json,
        DirectWriteOperation operation,
        string name,
        string? documentJson,
        int baseVersion,
        Func<RuleUpdateResult, IResult> written)
    {
        var author = PrincipalIdentity.Subject(http.User);
        var breakGlassActive = ResolveBreakGlass(http).Active(DateTimeOffset.UtcNow);
        var result = governance.DirectWrite(
            author, operation,
            new NewProposedChange(ChangeTargetKind.Rule, name, documentJson, baseVersion, null),
            breakGlassActive);

        if (result.Blocked is { } decision)
            return RefusedDirectWrite(decision, json);

        if (result.PublishedUnderBreakGlass)
            LogDirectWriteAudit(http, "rule", name, author);

        return written(result.Rule!);
    }

    /// <summary>
    /// Runs one direct proposition write — a create, an update, or a withdrawal — through the
    /// approval gate, and, when the gate allows it, through the very same core the ungoverned
    /// endpoint calls. See <see cref="GovernedRuleWrite"/>.
    /// </summary>
    /// <param name="governance">The governance workflow.</param>
    /// <param name="http">The request, for the caller's identity.</param>
    /// <param name="json">The serializer options every response on this surface is written with.</param>
    /// <param name="operation">Which ungoverned write this stands in for.</param>
    /// <param name="name">The proposition's dot-separated name.</param>
    /// <param name="documentJson">The proposed document, or null to withdraw.</param>
    /// <param name="baseVersion">The version the caller authored against; 0 for a creation.</param>
    /// <param name="modelTypeId">The model-type id, when the write creates a proposition.</param>
    /// <param name="description">The description, when the write creates a proposition.</param>
    /// <param name="written">Maps the write's own outcome onto this surface's response.</param>
    /// <returns>The gate's 403 refusal, or whatever <paramref name="written"/> produced.</returns>
    internal static IResult GovernedPropositionWrite(
        ChangeRequestSet governance,
        HttpContext http,
        JsonSerializerOptions json,
        DirectWriteOperation operation,
        string name,
        string? documentJson,
        int baseVersion,
        string? modelTypeId,
        string? description,
        Func<PropositionUpdateResult, IResult> written)
    {
        var author = PrincipalIdentity.Subject(http.User);
        var breakGlassActive = ResolveBreakGlass(http).Active(DateTimeOffset.UtcNow);
        var result = governance.DirectWrite(
            author, operation,
            new NewProposedChange(
                ChangeTargetKind.Proposition, name, documentJson, baseVersion, null,
                modelTypeId, description),
            breakGlassActive);

        if (result.Blocked is { } decision)
            return RefusedDirectWrite(decision, json);

        if (result.PublishedUnderBreakGlass)
            LogDirectWriteAudit(http, "proposition", name, author);

        return written(result.Proposition!);
    }

    /// <summary>
    /// The break-glass window in effect for this request. Resolved from DI rather than threaded
    /// through as a parameter, since every caller already carries the <see cref="HttpContext"/> this
    /// needs. Falls back to <see cref="BreakGlass.Off"/> when governance was enabled without a
    /// registration reaching this far — defensive only; <c>AddGovernance</c> always registers one.
    /// </summary>
    private static BreakGlass ResolveBreakGlass(HttpContext http) =>
        http.RequestServices.GetService<BreakGlass>() ?? BreakGlass.Off;

    /// <summary>
    /// The fixed category every break-glass bypass is logged through, so an operator can filter for
    /// it independently of which route emitted the line.
    /// </summary>
    private static ILogger AuditLog(HttpContext http) =>
        http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Motiv.Governance.Audit");

    /// <summary>The audit marker for a direct write that landed with the gate bypassed.</summary>
    private static void LogDirectWriteAudit(HttpContext http, string kind, string name, string author) =>
        AuditLog(http).LogWarning(
            "MOTIV-AUDIT break-glass publish: direct write of {Kind} '{Name}' by {Author} " +
            "published with the approval gate DISABLED.",
            kind, ForLog(name), ForLog(author));

    /// <summary>
    /// A caller-supplied value made safe for the audit trail: line breaks are stripped so a crafted
    /// artefact name or token subject cannot forge additional log lines (CWE-117). The audit log is
    /// the only record a break-glass direct write leaves, so its lines must be unforgeable.
    /// </summary>
    internal static string ForLog(string value) =>
        value.Replace("\r", string.Empty).Replace("\n", string.Empty);

    /// <summary>The gate's refusal as a 403, in the gate's own words.</summary>
    private static IResult Refused(GateDecision decision, JsonSerializerOptions json) =>
        Results.Json(
            new GateRefusalResponse(decision.Reason, decision.Assertions, decision.Justification),
            json, statusCode: 403);

    /// <summary>
    /// A refused direct write, whose reason also names the way forward. A refused *publish* has a
    /// change request the caller can go and get approved; a refused direct write mints nothing, so
    /// without this the caller is told only that they may not, never what to do instead.
    /// </summary>
    private static IResult RefusedDirectWrite(GateDecision decision, JsonSerializerOptions json) =>
        Results.Json(
            new GateRefusalResponse(
                $"{decision.Reason}. Raise the edit as a change request (POST change-requests) "
                + "and have it approved.",
                decision.Assertions,
                decision.Justification),
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
    /// A supplied document as raw JSON. An explicit <c>null</c> is "no document", which is how a
    /// removal is expressed; an absent property never reaches here, having been refused above.
    /// </summary>
    private static string? DocumentJson(JsonElement document) =>
        document.ValueKind == JsonValueKind.Null ? null : document.GetRawText();

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
                proposed.ModelTypeId,
                proposed.Description))],
            [.. change.Approvals.Select(approval => new ApprovalResponse(
                approval.Approver, approval.TimestampUtc, approval.Roles))]);

    /// <summary>
    /// Every non-Ok outcome as an HTTP answer. <paramref name="invalidState"/> is the whole sentence
    /// an invalid-state refusal answers with, so each transition can explain its own precondition
    /// rather than have one phrasing bent to fit all five. Defaulted, since not every caller's
    /// operation (e.g. <c>Create</c>) can ever produce an <see cref="ChangeRequestOutcome.InvalidState"/>
    /// outcome to word.
    /// </summary>
    private static IResult ToFailure(
        ChangeRequestResult result, JsonSerializerOptions json,
        string invalidState = "The change request is not publishable as authored.") =>
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
                    result.Change is { } change ? $"{invalidState} It is '{change.Status}'." : invalidState,
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
