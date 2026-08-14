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
/// <param name="Description">
/// The human-readable description, applied when creating a proposition and ignored otherwise — an
/// existing proposition keeps the description it was created with.
/// </param>
public sealed record NewProposedChange(
    ChangeTargetKind Kind,
    string Name,
    string? DocumentJson,
    int BaseVersion,
    int? RollbackOfVersion,
    string? ModelTypeId = null,
    string? Description = null);

/// <summary>
/// Which of the five ungoverned writes a <see cref="ChangeRequestSet.DirectWriteAsync"/> is standing in
/// for. Supplied rather than derived, because the caller's *intent* is what decides the refusal a
/// caller sees: authoring over a name that already carries a document is a name-taken conflict, not
/// a silent update, and the live state alone cannot tell the two apart.
/// </summary>
internal enum DirectWriteOperation
{
    /// <summary>Replace a rule's document.</summary>
    RuleUpdate,

    /// <summary>Return a rule to its default.</summary>
    RuleRevert,

    /// <summary>Author a proposition that has no document yet.</summary>
    PropositionCreate,

    /// <summary>Replace an authored proposition's document.</summary>
    PropositionUpdate,

    /// <summary>Withdraw an authored proposition.</summary>
    PropositionWithdraw
}

/// <summary>
/// The outcome of a <see cref="ChangeRequestSet.DirectWriteAsync"/>: either the gate refused, or the
/// underlying write ran and reported in its own terms. Exactly one of the three is non-null.
/// </summary>
/// <param name="Blocked">The gate's refusal, when it refused; otherwise null.</param>
/// <param name="Rule">The rule write's outcome, for the two rule operations.</param>
/// <param name="Proposition">The proposition write's outcome, for the three proposition operations.</param>
/// <param name="PublishedUnderBreakGlass">
/// Whether this write both bypassed an active break-glass window <em>and</em> genuinely succeeded —
/// the wrapped core reported a real success (a rule <see cref="RuleUpdateOutcome.Updated"/>; a
/// proposition <see cref="PropositionUpdateOutcome.Created"/>, <see cref="PropositionUpdateOutcome.Updated"/>,
/// or <see cref="PropositionUpdateOutcome.Removed"/>), not a stale version, an invalid document, or
/// an unknown target refused by the core itself. A direct write mints no <see cref="ChangeRequest"/>,
/// so there is nothing to stamp — this flag is the only record the caller has that the gate was
/// skipped, and is what tells the endpoint to emit the audit log; a write that failed after the gate
/// was skipped must not read as an audited publish that never happened. Always false when
/// <see cref="Blocked"/> is non-null.
/// </param>
internal sealed record DirectWriteResult(
    GateDecision? Blocked, RuleUpdateResult? Rule, PropositionUpdateResult? Proposition,
    bool PublishedUnderBreakGlass);

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
/// Two locks, deliberately: this set's own <see cref="_lock"/> guards the change-request list and
/// every request's own workflow state, and the shared <see cref="BindingScope"/> lock is taken once,
/// inside a publish, around the whole envelope. The scope lock is what makes an envelope atomic, so
/// it must not be taken and released per artefact. <see cref="_lock"/> is a <see cref="SemaphoreSlim"/>
/// rather than a plain <c>lock</c>, specifically so <see cref="PublishAsync"/> can hold it across the
/// <c>await</c> of the scope-locked apply — the whole publish is one critical section against
/// <see cref="Approve"/>/<see cref="Reject"/>/<see cref="Withdraw"/>, not two with a gap between them.
/// A <c>lock</c> statement cannot span an <c>await</c>, which is exactly the gap that would otherwise
/// let a concurrent rejection land mid-publish and go live anyway. Not reentrant: every method that
/// already holds it must call the non-acquiring <c>Core</c> counterpart, never a public method — see
/// <see cref="FindCore"/>.
/// </para>
/// </remarks>
public sealed class ChangeRequestSet
{
    private readonly ApprovalGate _gate;
    private readonly RuleSet _rules;
    private readonly PropositionSet? _propositions;
    private readonly SemaphoreSlim _lock = new(1, 1);
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
            _lock.Wait(CancellationToken.None);
            try
            {
                return [.. _changes];
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    /// <summary>Looks up a change request by id.</summary>
    /// <param name="id">The change request's identity.</param>
    /// <returns>The request, or null when the id is unknown.</returns>
    public ChangeRequest? Find(Guid id)
    {
        _lock.Wait(CancellationToken.None);
        try
        {
            return FindCore(id);
        }
        finally
        {
            _lock.Release();
        }
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
    /// <param name="changes">
    /// The edits that publish together. Must not be empty, and must target each artefact at most
    /// once — two edits to one target is authoring nonsense with no defensible reading (which wins?
    /// against which base version?), so it is refused here rather than discovered at publish.
    /// </param>
    /// <returns>The new request, or <see cref="ChangeRequestOutcome.Invalid"/> when the edits are unusable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="changes"/> is null.</exception>
    public ChangeRequestResult Create(string author, string changeNote, IReadOnlyList<NewProposedChange> changes)
    {
        if (changes is null) throw new ArgumentNullException(nameof(changes));

        if (changes.Count == 0)
            return Failure(ChangeRequestOutcome.Invalid, null, errors:
                [new RuleError("$", RuleErrorCode.InvalidNode, "a change request must propose at least one change")]);

        var targets = new HashSet<ChangeTarget>();
        foreach (var change in changes)
        {
            var duplicate = new ChangeTarget(change.Kind, change.Name);
            if (!targets.Add(duplicate))
                return Failure(ChangeRequestOutcome.Invalid, null, duplicate,
                    [new RuleError("$", RuleErrorCode.InvalidNode,
                        $"the change request targets {duplicate.Kind.ToString().ToLowerInvariant()} " +
                        $"'{duplicate.Name}' more than once")]);
        }

        // Classification reads live binding state — the layered source and the rules' documents —
        // so it runs under the scope lock, or a concurrent publish could be half-visible to it and
        // the request would be classified against a base that never existed.
        var proposed = _rules.Scope.Locked(() =>
        {
            var classified = new List<ProposedChange>(changes.Count);
            foreach (var change in changes)
            {
                var target = new ChangeTarget(change.Kind, change.Name);
                var current = CurrentStateOf(target);

                classified.Add(Classify(change, target, current));
            }

            return classified;
        });

        var request = new ChangeRequest(Guid.NewGuid(), author, changeNote, proposed);
        _lock.Wait(CancellationToken.None);
        try
        {
            _changes.Add(request);
        }
        finally
        {
            _lock.Release();
        }

        return Ok(request);
    }

    /// <summary>Records an approval, moving a draft request into review.</summary>
    /// <param name="id">The change request's identity.</param>
    /// <param name="approver">Who is approving.</param>
    /// <param name="roles">The roles the approver holds, captured as at the moment of approval.</param>
    /// <returns>The updated request, or why the approval was refused.</returns>
    public ChangeRequestResult Approve(Guid id, string approver, IReadOnlyList<string> roles)
    {
        _lock.Wait(CancellationToken.None);
        try
        {
            if (FindCore(id) is not { } change)
                return NotFound();

            if (!IsOpen(change))
                return Failure(ChangeRequestOutcome.InvalidState, change);

            change.AddApproval(new Approval(approver, DateTimeOffset.UtcNow, roles ?? []));
            return Ok(change);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Rejects a change request, terminating it with a reason.</summary>
    /// <param name="id">The change request's identity.</param>
    /// <param name="reason">Why the request is being rejected.</param>
    /// <returns>The updated request, or why the rejection was refused.</returns>
    public ChangeRequestResult Reject(Guid id, string reason)
    {
        _lock.Wait(CancellationToken.None);
        try
        {
            if (FindCore(id) is not { } change)
                return NotFound();

            if (!IsOpen(change))
                return Failure(ChangeRequestOutcome.InvalidState, change);

            change.MarkRejected(reason);
            return Ok(change);
        }
        finally
        {
            _lock.Release();
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
        _lock.Wait(CancellationToken.None);
        try
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
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Publishes a change request: gate first (unless break-glass), then the whole envelope applied
    /// atomically. Nothing is applied unless every edit validates.
    /// </summary>
    /// <remarks>
    /// Holds <see cref="_lock"/> for the whole method — status check, gate evaluation, the awaited
    /// apply, and <see cref="ChangeRequest.MarkPublished"/> — as one critical section. That is the
    /// entire reason <see cref="_lock"/> is a <see cref="SemaphoreSlim"/> rather than a plain C#
    /// <c>lock</c>: a <c>lock</c> block cannot contain an <c>await</c>, so splitting this method
    /// across two locked sections with the apply running unlocked in between would leave a window in
    /// which <see cref="Reject"/> or <see cref="Withdraw"/> could land on the very request being
    /// published — and unlike the version-conflict races the store's primary key already refuses,
    /// this one has no such backstop: a rejected request whose edits already went live and durable,
    /// durably recorded as "Rejected", with no audit trail of the publish that actually happened, is
    /// the one outcome a governed publish exists to make impossible. Holding <see cref="_lock"/> across
    /// the whole method removes the window instead of narrowing it.
    /// </remarks>
    /// <param name="id">The change request's identity.</param>
    /// <param name="breakGlassActive">
    /// Whether an active break-glass window is bypassing the gate. Bypassing is recorded on the
    /// request (<see cref="ChangeRequest.PublishedUnderBreakGlass"/>) — the ceremony is skipped, the
    /// fact that it was skipped is not.
    /// </param>
    /// <param name="cancellationToken">Cancels while waiting for the publish gate or the store.</param>
    /// <returns>The published request with each target's new version, or why publication was refused.</returns>
    public async Task<ChangeRequestResult> PublishAsync(
        Guid id, bool breakGlassActive, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
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

            var applied = await ChangeRequestPublisher.Apply(_rules, _propositions, change, cancellationToken)
                .ConfigureAwait(false);
            if (applied.Outcome != ChangeRequestOutcome.Ok)
                return applied with { Change = change };

            change.MarkPublished(breakGlassActive);
            return applied with { Change = change };
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Runs one ungoverned write through the approval gate without minting a change request: the
    /// edit is classified, offered to the gate as a transient <see cref="ChangeRequest"/>, and — if
    /// the gate allows it — executed by the very core the ungoverned endpoint would have called, so
    /// its caller reports the outcome in exactly the terms it always has.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what makes "no bypass" true without the ordinary rule and proposition surfaces
    /// growing a second vocabulary of refusals. A publish through <see cref="PublishAsync"/> answers in
    /// <see cref="ChangeRequestResult"/> terms, which cannot restate a referenced proposition's
    /// referrer list or a cascade's broken dependents; running the core itself returns those
    /// verbatim. There is exactly one execution — the gate decides, then the write happens.
    /// </para>
    /// <para>
    /// The whole span is under one <see cref="BindingScope"/> lock, so the world the gate was shown
    /// is the world the write lands in. Beyond that, the version compare-and-swap inside each core
    /// is what makes classification and application agree structurally: an edit authored against a
    /// version that has since moved fails the CAS and is refused as a conflict, in the same words as
    /// before.
    /// </para>
    /// <para>
    /// Nothing is recorded. A direct write is not a proposal, so it leaves no row in
    /// <see cref="All"/>; a caller whose write the gate refuses is pointed at <see cref="Create"/>,
    /// which is where proposals live.
    /// </para>
    /// </remarks>
    /// <param name="author">Who is performing the write.</param>
    /// <param name="operation">Which ungoverned write this stands in for.</param>
    /// <param name="change">The edit, whose <see cref="NewProposedChange.Kind"/> must match <paramref name="operation"/>.</param>
    /// <param name="breakGlassActive">
    /// Whether an active break-glass window is bypassing the gate. When true the gate is not
    /// evaluated at all — the write always proceeds — and the result's
    /// <see cref="DirectWriteResult.PublishedUnderBreakGlass"/> is set so the caller can audit-log it.
    /// </param>
    /// <param name="cancellationToken">Cancels while waiting for the publish gate or the store.</param>
    /// <returns>The gate's refusal, or the underlying write's own outcome.</returns>
    /// <exception cref="ArgumentException"><paramref name="change"/>'s kind contradicts <paramref name="operation"/>.</exception>
    /// <exception cref="InvalidOperationException">A proposition write was asked of a host with no <see cref="PropositionSet"/>.</exception>
    internal Task<DirectWriteResult> DirectWriteAsync(
        string author, DirectWriteOperation operation, NewProposedChange change, bool breakGlassActive = false,
        CancellationToken cancellationToken = default)
    {
        if (change is null) throw new ArgumentNullException(nameof(change));

        var kind = KindOf(operation);
        if (change.Kind != kind)
            throw new ArgumentException(
                $"A {operation} write targets a {kind.ToString().ToLowerInvariant()}, but the change " +
                $"names a {change.Kind.ToString().ToLowerInvariant()}.", nameof(change));

        if (kind == ChangeTargetKind.Proposition && _propositions is null)
            throw new InvalidOperationException(
                "This host has no PropositionSet, so a proposition cannot be written. The " +
                "proposition endpoints are not mounted without one, so reaching here is a wiring bug.");

        return _rules.Scope.LockedAsync(async () =>
        {
            var target = new ChangeTarget(kind, change.Name);
            var proposed = Classify(change, target, CurrentStateOf(target));
            var transient = new ChangeRequest(
                Guid.NewGuid(), author,
                $"direct {operation} of {kind.ToString().ToLowerInvariant()} '{change.Name}'",
                [proposed]);

            if (!breakGlassActive)
            {
                var decision = _gate.Evaluate(transient);
                if (!decision.MayPublish)
                    return new DirectWriteResult(decision, null, null, false);
            }

            return operation switch
            {
                DirectWriteOperation.RuleUpdate => OfRule(await UpdateCore(
                    _rules, change.Name, change.DocumentJson!, change.BaseVersion, cancellationToken)
                    .ConfigureAwait(false)),
                DirectWriteOperation.RuleRevert => OfRule(await RevertCore(
                    _rules, change.Name, change.BaseVersion, cancellationToken).ConfigureAwait(false)),
                DirectWriteOperation.PropositionCreate => OfProposition(
                    _propositions!.CreateCore(
                        change.Name, change.ModelTypeId!, change.DocumentJson!, change.Description)),
                DirectWriteOperation.PropositionUpdate => OfProposition(
                    _propositions!.UpdateCore(change.Name, change.DocumentJson!, change.BaseVersion)),
                _ => OfProposition(_propositions!.WithdrawCore(change.Name, change.BaseVersion))
            };
        }, cancellationToken);

        // breakGlassActive alone is not enough: it says the gate was skipped, not that the write
        // that followed actually landed. A stale base version, an invalid document, or an unknown
        // target still fails its own core the same way it would without break-glass — and for a
        // direct write the audit log is the *only* record, so a false positive here would be a log
        // entry for a publish that never happened. Only a genuine success sets it.
        DirectWriteResult OfRule(RuleUpdateResult result) =>
            new(null, result, null, breakGlassActive && result.Outcome == RuleUpdateOutcome.Updated);

        DirectWriteResult OfProposition(PropositionUpdateResult result) =>
            new(null, null, result, breakGlassActive && result.Outcome is
                PropositionUpdateOutcome.Created or PropositionUpdateOutcome.Updated or PropositionUpdateOutcome.Removed);
    }

    private static ChangeTargetKind KindOf(DirectWriteOperation operation) =>
        operation is DirectWriteOperation.RuleUpdate or DirectWriteOperation.RuleRevert
            ? ChangeTargetKind.Rule
            : ChangeTargetKind.Proposition;

    /// <summary>
    /// Bind → persist → commit for a caller already holding the <see cref="BindingScope"/> outer
    /// gate — the governance-side counterpart of <see cref="RuleSet.UpdateAsync"/>, calling the
    /// <c>Core</c> persist step directly rather than the public method, which would re-acquire the
    /// (non-reentrant) gate and self-deadlock. Attributed as <see cref="RuleChangeProvenance.System"/>
    /// for now; a later task replaces that with the change request's real author and approval
    /// reference, and batches every edit in an envelope into one store round trip instead of one
    /// per artefact.
    /// </summary>
    private static Task<RuleUpdateResult> UpdateCore(
        RuleSet rules, string name, string documentJson, int baseVersion, CancellationToken cancellationToken) =>
        rules.PersistAndCommitCoreAsync(
            name, () => rules.PrepareUpdateCore(name, documentJson, baseVersion),
            RuleChangeProvenance.System, cancellationToken);

    /// <summary>The revert companion to <see cref="UpdateCore"/>.</summary>
    private static Task<RuleUpdateResult> RevertCore(
        RuleSet rules, string name, int baseVersion, CancellationToken cancellationToken) =>
        rules.PersistAndCommitCoreAsync(
            name, () => rules.PrepareRevertCore(name, baseVersion),
            RuleChangeProvenance.System, cancellationToken);

    /// <summary>
    /// One edit classified against the target's current state. Shared by <see cref="Create"/> and
    /// <see cref="DirectWriteAsync"/> so a direct write is shown to the gate as exactly the change request
    /// an author would have raised for it. Assumes the scope lock is held.
    /// </summary>
    private ProposedChange Classify(
        NewProposedChange change, ChangeTarget target, (bool Exists, string? DocumentJson) current) =>
        new(target,
            change.DocumentJson,
            change.BaseVersion,
            ChangeClassifier.Classify(
                change.DocumentJson, current.DocumentJson, current.Exists, SpecIsAsync,
                change.RollbackOfVersion),
            change.ModelTypeId,
            change.Description);

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
    /// "not terminal", which is what <see cref="PublishAsync"/> asks: these three mirror exactly what
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
        public static Task<ChangeRequestResult> Apply(
            RuleSet rules, PropositionSet? propositions, ChangeRequest change,
            CancellationToken cancellationToken) =>
            rules.Scope.LockedAsync(
                async () => Validate(rules, propositions, change)
                    ?? await ApplyValidated(rules, propositions, change, cancellationToken).ConfigureAwait(false),
                cancellationToken);

        /// <summary>
        /// Walks the envelope in exactly the order <see cref="ApplyValidated"/> will, against a
        /// prospective overlay carrying the envelope's own edits — a rule edit may reference a
        /// proposition the same envelope creates, which the live source could not resolve.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Mirroring the apply order is load-bearing, not tidiness. Validating in envelope order
        /// while applying in canonical order lets the two disagree about the intermediate states:
        /// an envelope that creates a proposition referencing one it also withdraws validates
        /// against a world where the withdrawal already happened, then applies against one where it
        /// has not. Both passes must see the same sequence of worlds.
        /// </para>
        /// <para>
        /// Phase C runs withdrawals in envelope order, so an envelope that lists a proposition's
        /// withdrawal *before* the withdrawal of its only referrer is refused — at that point the
        /// referrer is still live. That is order sensitivity, not a bug: the apply does exactly the
        /// same thing, so validation agrees with it and nothing is applied. Reordering the two
        /// entries publishes cleanly. Phase C is deliberately not dependency-sorted; the sort would
        /// be new machinery to paper over an authoring order that the caller can simply reverse.
        /// </para>
        /// <para>
        /// A second, sharper limitation with the same shape: phase A prepares each dependent closure
        /// against the dependents' <em>live</em> documents, because that is what
        /// <c>PublishWithCascade</c> does at apply time. So an envelope that changes a proposition
        /// <em>and</em> edits the dependent rule to accommodate the change is refused, naming the
        /// very rule the envelope repairs — the rule is checked as it stands today, not as the
        /// envelope would leave it. Validation and apply agree, so nothing is applied and the
        /// refusal is safe; but the coordinated repair has to be split into two change requests, the
        /// rule's first. Closing this would mean teaching <c>PrepareClosure</c> to substitute the
        /// envelope's pending documents for the live ones, which is a change to the cascade engine
        /// itself rather than to this validator.
        /// </para>
        /// </remarks>
        /// <returns>The first failure found, or null when every edit would apply.</returns>
        private static ChangeRequestResult? Validate(
            RuleSet rules, PropositionSet? propositions, ChangeRequest change)
        {
            var prospective = new PropositionOverlay(rules.Scope.Overlay);
            var prospectiveSource = new LayeredSpecSource(prospective, rules.Scope.Registry);

            // What each republished node would reference once the envelope lands. The live graph
            // still holds these nodes' *old* edges, which phases A and B replace before any
            // withdrawal runs, so the referrer check in phase C has to prefer this. Keyed by NodeId
            // rather than by name: kind is part of a node's identity precisely because a host may
            // name a rule after a proposition, and merging the two would hide a real referrer.
            var rebound = new Dictionary<NodeId, IReadOnlyList<string>>();

            // Phase A — propositions coming into existence or changing.
            foreach (var proposed in Ordered(change, ChangeTargetKind.Proposition, deletions: false))
            {
                if (propositions is null)
                    return NoPropositionSet(proposed.Target);

                var name = proposed.Target.Name;
                var state = propositions.AuthoredStateCore(name);
                if (Mismatch(proposed, state.Exists, state.Version) is { } mismatch)
                    return mismatch;

                var modelTypeId = state.ModelTypeId ?? proposed.ModelTypeId;
                if (modelTypeId is null)
                    return Invalid(proposed.Target,
                        "creating a proposition requires a model-type id", RuleErrorCode.ModelTypeMismatch);

                // An existing proposition keeps its own description; a creation carries the one it
                // was authored with. Same precedence as the model-type id above, and the same
                // reason: what already exists cannot be restated into disagreement.
                var prepared = propositions.PrepareCore(
                    name, modelTypeId, proposed.ProposedDocumentJson!,
                    state.Description ?? proposed.Description, prospectiveSource);

                if (prepared.Entry is not { } entry)
                    return Failed(ChangeRequestOutcome.Invalid, proposed.Target, prepared.Errors);

                prospective.Set(entry);

                // The document binding on its own says nothing about what already resolves *through*
                // this name. PublishWithCascade rebinds the whole dependent closure and refuses the
                // publish if any member stops binding, so the same closure is prepared here, over the
                // prospective overlay, and a break is returned as a value rather than met at apply.
                if (rules.Scope.PrepareClosure(name, prospective, []) is { Count: > 0 } broken)
                    return BrokenDependents(proposed.Target, broken);

                rebound[NodeId.Proposition(name)] = prepared.References;
            }

            // Phase B — rules, which may reference anything phase A just folded in.
            foreach (var proposed in change.ProposedChanges)
            {
                if (proposed.Target.Kind != ChangeTargetKind.Rule)
                    continue;

                var name = proposed.Target.Name;

                // Rules are compiled-registered, so an unknown one cannot be created into existence.
                if (rules.FindEntry(name) is not { } entry)
                    return Failed(ChangeRequestOutcome.NotFound, proposed.Target, []);

                if (Mismatch(proposed, targetExists: true, entry.Version) is { } mismatch)
                    return mismatch;

                if (proposed.ProposedDocumentJson is null)
                {
                    // A revert re-binds the default against the world phase A just left, so it is
                    // not a return to something known-good: the proposition edit above may be
                    // exactly what stops the default binding.
                    var defaultErrors = rules.ValidateDefaultCore(name, prospectiveSource);
                    if (defaultErrors.Count > 0)
                        return Failed(ChangeRequestOutcome.Invalid, proposed.Target, defaultErrors);

                    // Not necessarily empty: a rule declared with a document default re-acquires
                    // that document's references when it reverts. Only a compiled default is a
                    // genuine departure from the graph.
                    rebound[NodeId.Rule(name)] = rules.DefaultReferencesOfCore(name);
                    continue;
                }

                var errors = rules.ValidateCore(name, proposed.ProposedDocumentJson, prospectiveSource);
                if (errors.Count > 0)
                    return Failed(ChangeRequestOutcome.Invalid, proposed.Target, errors);

                rebound[NodeId.Rule(name)] = rules.ReferencesOfCore(proposed.ProposedDocumentJson);
            }

            // Phase C — propositions going away, which nothing may still resolve through by then.
            var withdrawn = new HashSet<NodeId>();
            foreach (var proposed in Ordered(change, ChangeTargetKind.Proposition, deletions: true))
            {
                if (propositions is null)
                    return NoPropositionSet(proposed.Target);

                var name = proposed.Target.Name;
                var state = propositions.AuthoredStateCore(name);
                if (Mismatch(proposed, state.Exists, state.Version) is { } mismatch)
                    return mismatch;

                // The two arms WithdrawCore takes. With nothing compiled beneath the name, removal
                // would leave referrers dangling, so direct referrers block it outright; with a
                // compiled spec beneath, the name keeps resolving and the question is instead
                // whether every dependent still binds against what it reverts to.
                if (rules.Scope.Registry.Find(name) is null)
                {
                    var referrers = Referrers(rules, name, rebound, withdrawn);
                    if (referrers.Count > 0)
                        return Invalid(proposed.Target,
                            $"'{name}' is still referenced by {string.Join(", ", referrers)}");

                    prospective.Remove(name);
                }
                else
                {
                    prospective.Remove(name);
                    if (rules.Scope.PrepareClosure(name, prospective, []) is { Count: > 0 } broken)
                        return BrokenDependents(proposed.Target, broken);
                }

                withdrawn.Add(NodeId.Proposition(name));
            }

            return null;
        }

        /// <summary>
        /// Who would still reference <paramref name="name"/> at the moment the envelope withdraws
        /// it: the live graph's referrers, except those the envelope has already republished (whose
        /// old edges are gone) or already withdrawn, plus those whose *new* document references it.
        /// </summary>
        /// <remarks>
        /// The live graph alone is not enough in either direction. A proposition created earlier in
        /// this same envelope is not in it yet, and a rule whose edit drops its reference is still
        /// in it. Both are constructible envelopes, and both would otherwise pass validation and
        /// then be refused mid-apply.
        /// </remarks>
        private static IReadOnlyList<string> Referrers(
            RuleSet rules,
            string name,
            IReadOnlyDictionary<NodeId, IReadOnlyList<string>> rebound,
            // Not IReadOnlySet: netstandard2.0, which this assembly still targets, has no such type.
            HashSet<NodeId> withdrawn)
        {
            var referrers = new List<string>();

            foreach (var node in rules.Scope.Graph.Referrers(name))
            {
                if (!rebound.ContainsKey(node) && !withdrawn.Contains(node))
                    referrers.Add($"{node.KindLabel} '{node.Name}'");
            }

            foreach (var (node, references) in rebound)
            {
                if (!withdrawn.Contains(node) && references.Contains(name, StringComparer.Ordinal))
                    referrers.Add($"{node.KindLabel} '{node.Name}'");
            }

            return referrers;
        }

        /// <summary>
        /// A cascade refusal as a value. A broken dependent carries its own errors; flattening them
        /// keeps one error channel, and each is already prefixed with the dependent's name.
        /// </summary>
        private static ChangeRequestResult BrokenDependents(
            ChangeTarget target, IReadOnlyList<BrokenDependent> broken)
        {
            List<RuleError> errors =
            [
                .. broken.SelectMany(dependent => dependent.Errors.Select(error =>
                    new RuleError(error.Path, error.Code,
                        $"{dependent.Kind} '{dependent.Name}' would stop binding: {error.Message}")))
            ];

            // A dependent that failed to prepare without recording an error of its own would
            // otherwise flatten to nothing, turning a real refusal into an empty one. Cheap
            // insurance: name the dependents even when they will not say why.
            if (errors.Count == 0)
                errors.Add(new RuleError("$", RuleErrorCode.InvalidNode,
                    $"publishing '{target.Name}' would stop these binding: " +
                    string.Join(", ", broken.Select(dependent => $"{dependent.Kind} '{dependent.Name}'"))));

            return Failed(ChangeRequestOutcome.Invalid, target, errors);
        }

        /// <summary>
        /// Applies the validated envelope in the one order that lets its members reference each
        /// other: propositions coming into existence first, then the rules that may reference them,
        /// then the propositions going away — which nothing may still reference by then.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every refusal a core can produce has been validated away by the time this runs, so a
        /// refusal here is an invariant violation rather than an expected outcome, and it throws.
        /// Returning it as a value is the one thing that must never happen: the caller would report
        /// a clean refusal while some members of the envelope were already live and the change
        /// request sat wedged in Draft — applied edits with no governance record. Throwing under the
        /// lock leaves the same partially-applied state, but says so.
        /// </para>
        /// <para>
        /// Every refusal each core can produce has a counterpart in <see cref="Validate"/>:
        /// existence and version checks cover <c>NotFound</c>, <c>NameTaken</c> and
        /// <c>VersionConflict</c>; a prospective bind covers <c>Invalid</c>; the referrer check
        /// covers <c>Referenced</c>; and preparing the dependent closure over the prospective
        /// overlay — in phase A for a publish, in phase C for a withdrawal over a compiled spec —
        /// covers <c>BreaksDependents</c>, which is a cascade failure and therefore an ordinary
        /// expected outcome that must be returned as a value, never thrown.
        /// </para>
        /// </remarks>
        private static async Task<ChangeRequestResult> ApplyValidated(
            RuleSet rules, PropositionSet? propositions, ChangeRequest change,
            CancellationToken cancellationToken)
        {
            var versions = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var proposed in Ordered(change, ChangeTargetKind.Proposition, deletions: false))
            {
                var name = proposed.Target.Name;
                var state = propositions!.AuthoredStateCore(name);
                var result = state.Exists
                    ? propositions.UpdateCore(name, proposed.ProposedDocumentJson!, proposed.BaseVersion)
                    : propositions.CreateCore(
                        name, proposed.ModelTypeId!, proposed.ProposedDocumentJson!, proposed.Description);

                if (result.Outcome is not (PropositionUpdateOutcome.Created or PropositionUpdateOutcome.Updated))
                    throw Unexpected(proposed.Target, result.Outcome.ToString(), Detail(result));

                versions[name] = result.Version;
            }

            foreach (var proposed in change.ProposedChanges)
            {
                if (proposed.Target.Kind != ChangeTargetKind.Rule)
                    continue;

                var name = proposed.Target.Name;
                var result = proposed.ProposedDocumentJson is null
                    ? await RevertCore(rules, name, proposed.BaseVersion, cancellationToken).ConfigureAwait(false)
                    : await UpdateCore(rules, name, proposed.ProposedDocumentJson, proposed.BaseVersion, cancellationToken)
                        .ConfigureAwait(false);

                if (result.Outcome != RuleUpdateOutcome.Updated)
                    throw Unexpected(proposed.Target, result.Outcome.ToString(),
                        string.Join("; ", result.Errors));

                versions[name] = result.Version;
            }

            foreach (var proposed in Ordered(change, ChangeTargetKind.Proposition, deletions: true))
            {
                var result = propositions!.WithdrawCore(proposed.Target.Name, proposed.BaseVersion);
                if (result.Outcome != PropositionUpdateOutcome.Removed)
                    throw Unexpected(proposed.Target, result.Outcome.ToString(), Detail(result));

                // No authored document remains, so there is no version left to report.
                versions[proposed.Target.Name] = 0;
            }

            return new ChangeRequestResult(ChangeRequestOutcome.Ok, change, null, [], null, null, versions);
        }

        private static InvalidOperationException Unexpected(ChangeTarget target, string outcome, string detail) =>
            new($"Publishing {target.Kind.ToString().ToLowerInvariant()} '{target.Name}' was refused " +
                $"with '{outcome}' during the apply phase, after validation had accepted the whole " +
                $"change request. This is a bug in the publisher's validation, not a caller error. " +
                $"Earlier members of the envelope may already be live. Detail: {detail}");

        private static string Detail(PropositionUpdateResult result) =>
            string.Join("; ", [
                .. result.Errors.Select(error => error.ToString()),
                .. result.Referrers,
                .. result.BrokenDependents.Select(dependent =>
                    $"{dependent.Kind} '{dependent.Name}': {string.Join(", ", dependent.Errors)}")
            ]);

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

        private static ChangeRequestResult NoPropositionSet(ChangeTarget target) =>
            Invalid(target, "this host has no PropositionSet, so a proposition cannot be changed");

        private static ChangeRequestResult Invalid(
            ChangeTarget target, string message, RuleErrorCode code = RuleErrorCode.InvalidNode) =>
            Failed(ChangeRequestOutcome.Invalid, target, [new RuleError("$", code, message)]);

        private static ChangeRequestResult Failed(
            ChangeRequestOutcome outcome, ChangeTarget target, IReadOnlyList<RuleError> errors) =>
            new(outcome, null, null, errors, target, null, null);
    }
}
