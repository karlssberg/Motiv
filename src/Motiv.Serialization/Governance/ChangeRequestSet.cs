namespace Motiv.Serialization;

/// <summary>
/// One artefact edit as an author submits it, before classification. The classification a
/// <see cref="ProposedChange"/> carries is derived, not supplied — see
/// <see cref="ChangeRequestSet.Create"/> — so this is deliberately the smaller shape.
/// </summary>
/// <param name="Kind">Whether the target is a rule or a proposition.</param>
/// <param name="Name">The dot-separated target name.</param>
/// <param name="DocumentJson">
/// The proposed document, or null to delete the proposition / revert the rule to its default.
/// </param>
/// <param name="BaseVersion">
/// The version the edit was authored against — 0 when creating a proposition that does not exist
/// yet. A mismatch at publish time is a <see cref="ChangeRequestOutcome.VersionConflict"/>.
/// </param>
/// <param name="RollbackOfVersion">
/// The version this edit restores, when it is authored as a rollback. Stored intent: a rollback and
/// a coincidentally-identical authoring share a diff but are different governance events.
/// </param>
/// <param name="ModelTypeId">
/// The registered model-type id, required when creating a proposition and ignored otherwise.
/// </param>
public sealed record NewProposedChange(
    ChangeTargetKind Kind,
    string Name,
    string? DocumentJson,
    int BaseVersion,
    int? RollbackOfVersion,
    string? ModelTypeId = null);

/// <summary>What happened to a <see cref="ChangeRequestSet"/> operation.</summary>
public enum ChangeRequestOutcome
{
    /// <summary>The operation succeeded.</summary>
    Ok,

    /// <summary>No change request is known under the given id, or a target artefact is unknown.</summary>
    NotFound,

    /// <summary>
    /// The request's workflow state does not permit the operation — publishing a terminal request,
    /// or withdrawing someone else's.
    /// </summary>
    InvalidState,

    /// <summary>The approval gate refused publication; the decision is on the result.</summary>
    GateBlocked,

    /// <summary>A change's base version is stale; the current version is on the result.</summary>
    VersionConflict,

    /// <summary>A proposed document is malformed, would not bind, or is otherwise unpublishable.</summary>
    Invalid
}

/// <summary>The outcome of a <see cref="ChangeRequestSet"/> operation. Expected failures are values, not exceptions.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Change">The change request the operation concerned, or null when it was not found.</param>
/// <param name="Gate">The gate's decision, when <paramref name="Outcome"/> is <see cref="ChangeRequestOutcome.GateBlocked"/>.</param>
/// <param name="Errors">Why a document was rejected, when <paramref name="Outcome"/> is <see cref="ChangeRequestOutcome.Invalid"/>; otherwise empty.</param>
/// <param name="FailedTarget">Which target failed validation, when one did; otherwise null.</param>
/// <param name="ConflictVersion">The target's current version, when <paramref name="Outcome"/> is <see cref="ChangeRequestOutcome.VersionConflict"/>.</param>
/// <param name="PublishedVersions">
/// Each published target's new version, keyed by target name, on a successful publish; otherwise
/// null. A withdrawn proposition reports 0 — it no longer has an authored document.
/// </param>
public sealed record ChangeRequestResult(
    ChangeRequestOutcome Outcome,
    ChangeRequest? Change,
    GateDecision? Gate,
    IReadOnlyList<RuleError> Errors,
    ChangeTarget? FailedTarget,
    int? ConflictVersion,
    IReadOnlyDictionary<string, int>? PublishedVersions);

/// <summary>
/// The governance workflow: change requests are created, approved or rejected, and published as one
/// atomic envelope through the <see cref="ApprovalGate"/>.
/// </summary>
/// <remarks>
/// <para>
/// Grant-agnostic by design. Who may author, approve, or break the glass is an authorisation
/// question answered at the HTTP layer; this type answers only "does the workflow permit it" and
/// "does the gate permit it". The one caller identity it does consult is the author of a withdrawal,
/// because authorship is workflow state, not a grant.
/// </para>
/// <para>
/// Two locks, deliberately: this set's own lock guards the change-request list, and the shared
/// <see cref="BindingScope"/> lock is taken once, inside a publish, around the whole envelope. The
/// scope lock is what makes an envelope atomic, so it must not be taken and released per artefact.
/// </para>
/// </remarks>
public sealed class ChangeRequestSet
{
    private readonly ApprovalGate _gate;
    private readonly RuleSet _rules;
    private readonly PropositionSet? _propositions;
    private readonly object _lock = new();
    private readonly List<ChangeRequest> _changes = [];

    /// <summary>Creates the governance workflow over a rule set and, optionally, a proposition set.</summary>
    /// <param name="gate">The may-publish policy every non-break-glass publish is checked against.</param>
    /// <param name="rules">The live rules an envelope may edit.</param>
    /// <param name="propositions">The live authored propositions an envelope may edit, or null when the host authors none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="gate"/> or <paramref name="rules"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The two sets do not share a <see cref="BindingScope"/>. Atomicity across an envelope is one
    /// lock held over every edit in it; two scopes are two locks, so a rule edit and a proposition
    /// edit could interleave with someone else's publish no matter what this type did. Build the
    /// rule set from the proposition set (<c>AddMotivRules().AddPropositions()</c> does) so the two
    /// share one scope.
    /// </exception>
    public ChangeRequestSet(ApprovalGate gate, RuleSet rules, PropositionSet? propositions)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));

        if (propositions is not null && !ReferenceEquals(propositions.Scope, rules.Scope))
            throw new InvalidOperationException(
                "The RuleSet and PropositionSet given to a ChangeRequestSet must share one " +
                "BindingScope, otherwise a change request spanning both could not publish " +
                "atomically. Build the RuleSet from the PropositionSet.");

        _propositions = propositions;
    }

    /// <summary>Every change request, in creation order.</summary>
    public IReadOnlyList<ChangeRequest> All
    {
        get
        {
            lock (_lock)
                return [.. _changes];
        }
    }

    /// <summary>Looks up a change request by id.</summary>
    /// <param name="id">The change request's identity.</param>
    /// <returns>The request, or null when the id is unknown.</returns>
    public ChangeRequest? Find(Guid id)
    {
        lock (_lock)
            return FindCore(id);
    }

    /// <summary>
    /// Records a new change request in <see cref="ChangeRequestStatus.Draft"/>, classifying each
    /// edit against the artefact's current document.
    /// </summary>
    /// <remarks>
    /// Classification is computed here rather than at publish time on purpose: it is what the gate
    /// reasons over and what reviewers saw, so it must describe the change as authored. A base
    /// document that moves underneath the request afterwards is caught by the version check at
    /// publish, not by silently reclassifying.
    /// </remarks>
    /// <param name="author">Who is authoring the request.</param>
    /// <param name="changeNote">A human-readable note describing the change.</param>
    /// <param name="changes">The edits that publish together. Must not be empty.</param>
    /// <returns>The new request, or <see cref="ChangeRequestOutcome.Invalid"/> when no edits were given.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="changes"/> is null.</exception>
    public ChangeRequestResult Create(string author, string changeNote, IReadOnlyList<NewProposedChange> changes)
    {
        if (changes is null) throw new ArgumentNullException(nameof(changes));

        if (changes.Count == 0)
            return Failure(ChangeRequestOutcome.Invalid, null, errors:
                [new RuleError("$", RuleErrorCode.InvalidNode, "a change request must propose at least one change")]);

        var proposed = new List<ProposedChange>(changes.Count);
        foreach (var change in changes)
        {
            var target = new ChangeTarget(change.Kind, change.Name);
            var current = CurrentStateOf(target);

            proposed.Add(new ProposedChange(
                target,
                change.DocumentJson,
                change.BaseVersion,
                ChangeClassifier.Classify(
                    change.DocumentJson, current.DocumentJson, current.Exists, SpecIsAsync, change.RollbackOfVersion),
                change.ModelTypeId));
        }

        var request = new ChangeRequest(Guid.NewGuid(), author, changeNote, proposed);
        lock (_lock)
            _changes.Add(request);

        return Ok(request);
    }

    /// <summary>Records an approval, moving a draft request into review.</summary>
    /// <param name="id">The change request's identity.</param>
    /// <param name="approver">Who is approving.</param>
    /// <param name="roles">The roles the approver holds, captured as at the moment of approval.</param>
    /// <returns>The updated request, or why the approval was refused.</returns>
    public ChangeRequestResult Approve(Guid id, string approver, IReadOnlyList<string> roles)
    {
        lock (_lock)
        {
            if (FindCore(id) is not { } change)
                return NotFound();

            if (!IsOpen(change))
                return Failure(ChangeRequestOutcome.InvalidState, change);

            change.AddApproval(new Approval(approver, DateTimeOffset.UtcNow, roles ?? []));
            return Ok(change);
        }
    }

    /// <summary>Rejects a change request, terminating it with a reason.</summary>
    /// <param name="id">The change request's identity.</param>
    /// <param name="reason">Why the request is being rejected.</param>
    /// <returns>The updated request, or why the rejection was refused.</returns>
    public ChangeRequestResult Reject(Guid id, string reason)
    {
        lock (_lock)
        {
            if (FindCore(id) is not { } change)
                return NotFound();

            if (!IsOpen(change))
                return Failure(ChangeRequestOutcome.InvalidState, change);

            change.MarkRejected(reason);
            return Ok(change);
        }
    }

    /// <summary>Withdraws a change request. Only its author may withdraw it.</summary>
    /// <param name="id">The change request's identity.</param>
    /// <param name="caller">Who is asking to withdraw it.</param>
    /// <returns>
    /// The updated request, or <see cref="ChangeRequestOutcome.InvalidState"/> when the caller is
    /// not the author or the request is already terminal.
    /// </returns>
    public ChangeRequestResult Withdraw(Guid id, string caller)
    {
        lock (_lock)
        {
            if (FindCore(id) is not { } change)
                return NotFound();

            // Authorship is workflow state rather than a grant, so this one identity check belongs
            // here: withdrawal is the author retracting their own proposal, and a third party doing
            // it is a rejection, which has its own method and its own audit meaning.
            if (!IsOpen(change) || change.Author != caller)
                return Failure(ChangeRequestOutcome.InvalidState, change);

            change.MarkWithdrawn();
            return Ok(change);
        }
    }

    /// <summary>
    /// Publishes a change request: gate first (unless break-glass), then the whole envelope applied
    /// atomically. Nothing is applied unless every edit validates.
    /// </summary>
    /// <param name="id">The change request's identity.</param>
    /// <param name="breakGlassActive">
    /// Whether an active break-glass window is bypassing the gate. Bypassing is recorded on the
    /// request (<see cref="ChangeRequest.PublishedUnderBreakGlass"/>) — the ceremony is skipped, the
    /// fact that it was skipped is not.
    /// </param>
    /// <returns>The published request with each target's new version, or why publication was refused.</returns>
    public ChangeRequestResult Publish(Guid id, bool breakGlassActive)
    {
        lock (_lock)
        {
            if (FindCore(id) is not { } change)
                return NotFound();

            if (change.Status is ChangeRequestStatus.Published
                or ChangeRequestStatus.Rejected
                or ChangeRequestStatus.Withdrawn)
                return Failure(ChangeRequestOutcome.InvalidState, change);

            if (!breakGlassActive)
            {
                var decision = _gate.Evaluate(change);
                if (!decision.MayPublish)
                    return new ChangeRequestResult(
                        ChangeRequestOutcome.GateBlocked, change, decision, [], null, null, null);
            }

            var applied = ChangeRequestPublisher.Apply(_rules, _propositions, change);
            if (applied.Outcome != ChangeRequestOutcome.Ok)
                return applied with { Change = change };

            change.MarkPublished(breakGlassActive);
            return applied with { Change = change };
        }
    }

    /// <summary>
    /// Whether a spec name is evaluated asynchronously, read from the binding scope's live layered
    /// source — authored propositions first, then compiled specs. That source is the same one a
    /// document actually binds against, so it is the only answer that cannot disagree with the bind;
    /// a catalogue listing would be a second copy of it, and works only when a proposition set exists.
    /// </summary>
    private bool SpecIsAsync(string name) => _rules.Scope.Source.Find(name) is { IsAsync: true };

    /// <summary>The target's current document and whether it exists at all, as classification sees it.</summary>
    private (bool Exists, string? DocumentJson) CurrentStateOf(ChangeTarget target)
    {
        if (target.Kind == ChangeTargetKind.Rule)
            return _rules.FindEntry(target.Name) is { } entry ? (true, entry.DocumentJson) : (false, null);

        var authored = _propositions?.DocumentJsonOf(target.Name);
        return (authored is not null, authored);
    }

    private ChangeRequest? FindCore(Guid id) => _changes.FirstOrDefault(change => change.Id == id);

    /// <summary>
    /// Whether the request still accepts approve/reject/withdraw. Deliberately narrower than
    /// "not terminal", which is what <see cref="Publish"/> asks: these three mirror exactly what
    /// <see cref="ChangeRequest"/>'s own transitions accept, so a refusal is a returned outcome
    /// rather than an exception thrown from inside the lock.
    /// </summary>
    private static bool IsOpen(ChangeRequest change) =>
        change.Status is ChangeRequestStatus.Draft or ChangeRequestStatus.InReview;

    private static ChangeRequestResult Ok(ChangeRequest change) =>
        new(ChangeRequestOutcome.Ok, change, null, [], null, null, null);

    private static ChangeRequestResult NotFound() =>
        new(ChangeRequestOutcome.NotFound, null, null, [], null, null, null);

    private static ChangeRequestResult Failure(
        ChangeRequestOutcome outcome,
        ChangeRequest? change,
        ChangeTarget? target = null,
        IReadOnlyList<RuleError>? errors = null,
        int? conflictVersion = null) =>
        new(outcome, change, null, errors ?? [], target, conflictVersion, null);

    /// <summary>
    /// Applies a whole change request under one <see cref="BindingScope"/> lock: validate every
    /// edit, then apply every edit. All-validate-then-all-apply is the atomicity mechanism — a
    /// refusal happens while nothing has moved, so a rejected envelope leaves no half-published
    /// state behind.
    /// </summary>
    private static class ChangeRequestPublisher
    {
        public static ChangeRequestResult Apply(RuleSet rules, PropositionSet? propositions, ChangeRequest change) =>
            rules.Scope.Locked(() => Validate(rules, propositions, change) ?? ApplyValidated(rules, propositions, change));

        /// <summary>
        /// Checks every edit against the live state and against a prospective source carrying the
        /// envelope's own proposition edits — a rule edit may reference a proposition the same
        /// envelope creates, which the live source could not resolve.
        /// </summary>
        /// <returns>The first failure found, or null when every edit would apply.</returns>
        private static ChangeRequestResult? Validate(
            RuleSet rules, PropositionSet? propositions, ChangeRequest change)
        {
            var prospective = new PropositionOverlay(rules.Scope.Overlay);
            var prospectiveSource = new LayeredSpecSource(prospective, rules.Scope.Registry);

            // Propositions first, folding each one into the prospective overlay, so that by the time
            // the rules are checked the overlay describes the state the envelope would leave behind.
            foreach (var proposed in change.ProposedChanges)
            {
                if (proposed.Target.Kind != ChangeTargetKind.Proposition)
                    continue;

                if (propositions is null)
                    return Invalid(proposed.Target,
                        "this host has no PropositionSet, so a proposition cannot be changed");

                var state = propositions.AuthoredStateCore(proposed.Target.Name);
                if (Mismatch(proposed, state.Exists, state.Version) is { } mismatch)
                    return mismatch;

                if (proposed.ProposedDocumentJson is null)
                {
                    prospective.Remove(proposed.Target.Name);
                    continue;
                }

                var modelTypeId = state.ModelTypeId ?? proposed.ModelTypeId;
                if (modelTypeId is null)
                    return Invalid(proposed.Target,
                        "creating a proposition requires a model-type id", RuleErrorCode.ModelTypeMismatch);

                var prepared = propositions.PrepareCore(
                    proposed.Target.Name, modelTypeId, proposed.ProposedDocumentJson,
                    state.Description, prospectiveSource);

                if (prepared.Entry is not { } entry)
                    return Failed(ChangeRequestOutcome.Invalid, proposed.Target, prepared.Errors);

                prospective.Set(entry);
            }

            foreach (var proposed in change.ProposedChanges)
            {
                if (proposed.Target.Kind != ChangeTargetKind.Rule)
                    continue;

                // Rules are compiled-registered, so an unknown one cannot be created into existence.
                if (rules.FindEntry(proposed.Target.Name) is not { } entry)
                    return Failed(ChangeRequestOutcome.NotFound, proposed.Target, []);

                if (Mismatch(proposed, targetExists: true, entry.Version) is { } mismatch)
                    return mismatch;

                if (proposed.ProposedDocumentJson is null)
                    continue;

                var errors = rules.ValidateCore(
                    proposed.Target.Name, proposed.ProposedDocumentJson, prospectiveSource);

                if (errors.Count > 0)
                    return Failed(ChangeRequestOutcome.Invalid, proposed.Target, errors);
            }

            return null;
        }

        /// <summary>
        /// Applies the validated envelope in the one order that lets its members reference each
        /// other: propositions coming into existence first, then the rules that may reference them,
        /// then the propositions going away — which nothing may still reference by then.
        /// </summary>
        /// <remarks>
        /// A cascade check inside a core can still refuse an edit here, after earlier members have
        /// applied — a withdrawal blocked by a referrer outside the envelope is the realistic case.
        /// The validation pass cannot see that, because the referrer's post-envelope state is not
        /// live yet. Such a failure is reported like any other, but unlike a validation failure it
        /// leaves the earlier members of the envelope published.
        /// </remarks>
        private static ChangeRequestResult ApplyValidated(
            RuleSet rules, PropositionSet? propositions, ChangeRequest change)
        {
            var versions = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var proposed in Ordered(change, ChangeTargetKind.Proposition, deletions: false))
            {
                var name = proposed.Target.Name;
                var state = propositions!.AuthoredStateCore(name);
                var result = state.Exists
                    ? propositions.UpdateCore(name, proposed.ProposedDocumentJson!, proposed.BaseVersion)
                    : propositions.CreateCore(
                        name, proposed.ModelTypeId!, proposed.ProposedDocumentJson!, description: null);

                if (result.Outcome is not (PropositionUpdateOutcome.Created or PropositionUpdateOutcome.Updated))
                    return MapProposition(result, proposed.Target);

                versions[name] = result.Version;
            }

            foreach (var proposed in change.ProposedChanges)
            {
                if (proposed.Target.Kind != ChangeTargetKind.Rule)
                    continue;

                var name = proposed.Target.Name;
                var result = proposed.ProposedDocumentJson is null
                    ? rules.RevertCore(name, proposed.BaseVersion)
                    : rules.UpdateCore(name, proposed.ProposedDocumentJson, proposed.BaseVersion);

                if (result.Outcome != RuleUpdateOutcome.Updated)
                    return MapRule(result, proposed.Target);

                versions[name] = result.Version;
            }

            foreach (var proposed in Ordered(change, ChangeTargetKind.Proposition, deletions: true))
            {
                var result = propositions!.WithdrawCore(proposed.Target.Name, proposed.BaseVersion);
                if (result.Outcome != PropositionUpdateOutcome.Removed)
                    return MapProposition(result, proposed.Target);

                // No authored document remains, so there is no version left to report.
                versions[proposed.Target.Name] = 0;
            }

            return new ChangeRequestResult(ChangeRequestOutcome.Ok, change, null, [], null, null, versions);
        }

        private static IEnumerable<ProposedChange> Ordered(
            ChangeRequest change, ChangeTargetKind kind, bool deletions) =>
            change.ProposedChanges.Where(proposed =>
                proposed.Target.Kind == kind && (proposed.ProposedDocumentJson is null) == deletions);

        /// <summary>
        /// Whether the target's live state contradicts the edit: a creation landing on a name that
        /// already exists, an edit to one that does not, or a stale base version.
        /// </summary>
        private static ChangeRequestResult? Mismatch(ProposedChange proposed, bool targetExists, int currentVersion)
        {
            if (proposed.Classification.IsCreation && targetExists)
                return Invalid(proposed.Target, $"'{proposed.Target.Name}' already exists, so it cannot be created");

            if (!proposed.Classification.IsCreation && !targetExists)
                return Failed(ChangeRequestOutcome.NotFound, proposed.Target, []);

            return proposed.BaseVersion == currentVersion
                ? null
                : new ChangeRequestResult(
                    ChangeRequestOutcome.VersionConflict, null, null, [], proposed.Target, currentVersion, null);
        }

        private static ChangeRequestResult MapRule(RuleUpdateResult result, ChangeTarget target) =>
            result.Outcome switch
            {
                RuleUpdateOutcome.VersionConflict => new ChangeRequestResult(
                    ChangeRequestOutcome.VersionConflict, null, null, [], target, result.Version, null),
                RuleUpdateOutcome.NotFound => Failed(ChangeRequestOutcome.NotFound, target, []),
                _ => Failed(ChangeRequestOutcome.Invalid, target, result.Errors)
            };

        private static ChangeRequestResult MapProposition(PropositionUpdateResult result, ChangeTarget target) =>
            result.Outcome switch
            {
                PropositionUpdateOutcome.VersionConflict => new ChangeRequestResult(
                    ChangeRequestOutcome.VersionConflict, null, null, [], target, result.Version, null),
                PropositionUpdateOutcome.NotFound => Failed(ChangeRequestOutcome.NotFound, target, []),
                PropositionUpdateOutcome.Referenced => Invalid(target,
                    $"'{target.Name}' is still referenced by {string.Join(", ", result.Referrers)}"),
                PropositionUpdateOutcome.NameTaken => Invalid(target,
                    $"'{target.Name}' is already authored"),
                // A broken dependent carries its own errors; flattening them keeps one error channel.
                _ => Failed(ChangeRequestOutcome.Invalid, target,
                    [.. result.Errors, .. result.BrokenDependents.SelectMany(dependent => dependent.Errors)])
            };

        private static ChangeRequestResult Invalid(
            ChangeTarget target, string message, RuleErrorCode code = RuleErrorCode.InvalidNode) =>
            Failed(ChangeRequestOutcome.Invalid, target, [new RuleError("$", code, message)]);

        private static ChangeRequestResult Failed(
            ChangeRequestOutcome outcome, ChangeTarget target, IReadOnlyList<RuleError> errors) =>
            new(outcome, null, null, errors, target, null, null);
    }
}
