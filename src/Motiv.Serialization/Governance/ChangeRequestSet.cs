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
    Invalid,

    /// <summary>
    /// The rule half of an envelope's persist phase durably succeeded, but the proposition half then
    /// failed — an infrastructure fault (the store threw), not a version conflict, since
    /// <see cref="IPropositionStore.WriteAsync"/> has no compare-and-set to lose. Nothing is live for
    /// either half — the apply phase never runs — but the rule store now holds a row this process's
    /// live state does not reflect, and a bare retry of the same request would re-prepare against the
    /// unchanged live version and collide with that row's primary key, refusing forever. This outcome
    /// exists so that collision is never silently mistaken for an ordinary retryable conflict: the
    /// caller must restart the process (which reloads both stores and quarantines anything that no
    /// longer binds) rather than retry this request in place.
    /// </summary>
    PersistenceDesynced
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
/// Three locks, deliberately. The shared <see cref="BindingScope"/> lock is taken once, inside a
/// publish, around the whole envelope — it is what makes an envelope atomic, so it must not be taken
/// and released per artefact. <see cref="_lock"/> guards one request's own workflow-transition
/// atomicity — <see cref="Approve"/>/<see cref="Reject"/>/<see cref="Withdraw"/>/<see cref="PublishAsync"/>
/// must serialise against each other on the <em>same</em> request, or a rejection could land while
/// its publish is mid-flight (see the remarks on <see cref="PublishAsync"/>). It is a
/// <see cref="SemaphoreSlim"/> rather than a plain <c>lock</c> specifically so <c>PublishAsync</c> can
/// hold it across the <c>await</c> of the scope-locked apply — a <c>lock</c> statement cannot span an
/// <c>await</c>. Not reentrant: every method that already holds it must call the non-acquiring
/// <c>Core</c> counterpart, never a public method.
/// </para>
/// <para>
/// <see cref="_listLock"/> is the third, separate from <see cref="_lock"/> on purpose: it guards only
/// <see cref="_changes"/>'s own structural integrity (a plain <see cref="List{T}"/> is not
/// thread-safe for concurrent enumeration and <c>Add</c>), not any request's workflow state. Reading
/// the list — <see cref="All"/>, <see cref="Find"/>, and every <c>Core</c> method's own
/// <see cref="FindCore"/> lookup — never needs to wait behind a publish that is stuck on a slow
/// store; only <c>Create</c>'s append and every lookup take it, and both are always instantaneous, so
/// it is never held across an <c>await</c> either. This intentionally does not synchronise reads of
/// a request's own mutable fields (<see cref="ChangeRequest.Status"/> and friends) against a
/// concurrent <see cref="_lock"/>-held mutation of them — <see cref="All"/>/<see cref="Find"/> can
/// observe a request mid-transition. Narrower than the memory-visibility gap this class had before
/// two locks existed (everything shared one lock, so every read was synchronised with every write),
/// but accepted for the same reason <see cref="RuleBase.Quarantine"/> was: an availability guarantee
/// — the read surface must not go unresponsive behind a stalled store — outweighs strict
/// linearizability here. A <see cref="System.Threading.Volatile"/>-based fix mirroring
/// <c>RuleBase.Quarantine</c>'s would close it if the gap is ever load-bearing rather than cosmetic.
/// </para>
/// </remarks>
public sealed class ChangeRequestSet
{
    private readonly ApprovalGate _gate;
    private readonly RuleSet _rules;
    private readonly PropositionSet? _propositions;

    // Deliberately never disposed, and this class is deliberately not IDisposable: it lives for the
    // host's lifetime (typically a DI singleton, alongside the RuleSet/PropositionSet it publishes
    // into, neither of which is disposable either), and SemaphoreSlim's Dispose only matters if
    // AvailableWaitHandle was ever called — which nothing here does. Same choice BindingScope's own
    // outer SemaphoreSlim already made.
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly object _listLock = new();
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

    /// <summary>
    /// Every change request, in creation order. Reads only <see cref="_listLock"/> — never
    /// <see cref="_lock"/> — so this never waits behind a publish, however long its store round trip
    /// takes.
    /// </summary>
    public IReadOnlyList<ChangeRequest> All
    {
        get
        {
            lock (_listLock)
                return [.. _changes];
        }
    }

    /// <summary>
    /// Looks up a change request by id. Reads only <see cref="_listLock"/> — see <see cref="All"/>.
    /// </summary>
    /// <param name="id">The change request's identity.</param>
    /// <returns>The request, or null when the id is unknown.</returns>
    public ChangeRequest? Find(Guid id) => FindCore(id);

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

        // Only the list itself needs guarding here — a brand-new request cannot yet race any
        // workflow transition on itself — so this takes _listLock, not _lock; see the class remarks.
        var request = new ChangeRequest(Guid.NewGuid(), author, changeNote, proposed);
        lock (_listLock)
            _changes.Add(request);

        return Ok(request);
    }

    /// <summary>
    /// Records an approval, moving a draft request into review. Blocks the calling thread while
    /// <see cref="_lock"/> is held by a publish's store round trip — pass <paramref name="cancellationToken"/>
    /// when one is available (an aborted HTTP request should not park a thread-pool thread), or use
    /// <see cref="ApproveAsync"/> from an async caller instead.
    /// </summary>
    /// <param name="id">The change request's identity.</param>
    /// <param name="approver">Who is approving.</param>
    /// <param name="roles">The roles the approver holds, captured as at the moment of approval.</param>
    /// <param name="cancellationToken">Cancels the wait for <see cref="_lock"/>.</param>
    /// <returns>The updated request, or why the approval was refused.</returns>
    public ChangeRequestResult Approve(
        Guid id, string approver, IReadOnlyList<string> roles, CancellationToken cancellationToken = default)
    {
        // Blocking wait: _lock can be held for a whole publish's store round trip, so this parks the
        // calling thread for however long that takes. Acceptable for a synchronous caller with no
        // event loop to give back (tests, direct library use); an ASP.NET Core handler should prefer
        // ApproveAsync so the request thread is released back to the pool while it waits.
        _lock.Wait(cancellationToken);
        try
        {
            return ApproveCore(id, approver, roles);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>The async counterpart to <see cref="Approve"/>, for callers with an event loop to give back while waiting.</summary>
    public async Task<ChangeRequestResult> ApproveAsync(
        Guid id, string approver, IReadOnlyList<string> roles, CancellationToken cancellationToken = default)
    {
        // Waits on _lock, which a concurrent publish may hold for its whole store round trip;
        // await-based, so this frees the calling thread back to the pool rather than parking it.
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return ApproveCore(id, approver, roles);
        }
        finally
        {
            _lock.Release();
        }
    }

    private ChangeRequestResult ApproveCore(Guid id, string approver, IReadOnlyList<string> roles)
    {
        if (FindCore(id) is not { } change)
            return NotFound();

        if (!IsOpen(change))
            return Failure(ChangeRequestOutcome.InvalidState, change);

        change.AddApproval(new Approval(approver, DateTimeOffset.UtcNow, roles ?? []));
        return Ok(change);
    }

    /// <summary>
    /// Rejects a change request, terminating it with a reason. See <see cref="Approve"/>'s remarks on
    /// blocking and cancellation — the same apply here.
    /// </summary>
    /// <param name="id">The change request's identity.</param>
    /// <param name="reason">Why the request is being rejected.</param>
    /// <param name="cancellationToken">Cancels the wait for <see cref="_lock"/>.</param>
    /// <returns>The updated request, or why the rejection was refused.</returns>
    public ChangeRequestResult Reject(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        // Blocking wait — see Approve's remark: _lock may be held for a publish's whole store round trip.
        _lock.Wait(cancellationToken);
        try
        {
            return RejectCore(id, reason);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>The async counterpart to <see cref="Reject"/>. See <see cref="ApproveAsync"/>.</summary>
    public async Task<ChangeRequestResult> RejectAsync(
        Guid id, string reason, CancellationToken cancellationToken = default)
    {
        // Waits on _lock — see ApproveAsync's remark.
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return RejectCore(id, reason);
        }
        finally
        {
            _lock.Release();
        }
    }

    private ChangeRequestResult RejectCore(Guid id, string reason)
    {
        if (FindCore(id) is not { } change)
            return NotFound();

        if (!IsOpen(change))
            return Failure(ChangeRequestOutcome.InvalidState, change);

        change.MarkRejected(reason);
        return Ok(change);
    }

    /// <summary>
    /// Withdraws a change request. Only its author may withdraw it. See <see cref="Approve"/>'s
    /// remarks on blocking and cancellation — the same apply here.
    /// </summary>
    /// <param name="id">The change request's identity.</param>
    /// <param name="caller">Who is asking to withdraw it.</param>
    /// <param name="cancellationToken">Cancels the wait for <see cref="_lock"/>.</param>
    /// <returns>
    /// The updated request, or <see cref="ChangeRequestOutcome.InvalidState"/> when the caller is
    /// not the author or the request is already terminal.
    /// </returns>
    public ChangeRequestResult Withdraw(Guid id, string caller, CancellationToken cancellationToken = default)
    {
        // Blocking wait — see Approve's remark: _lock may be held for a publish's whole store round trip.
        _lock.Wait(cancellationToken);
        try
        {
            return WithdrawCore(id, caller);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>The async counterpart to <see cref="Withdraw"/>. See <see cref="ApproveAsync"/>.</summary>
    public async Task<ChangeRequestResult> WithdrawAsync(
        Guid id, string caller, CancellationToken cancellationToken = default)
    {
        // Waits on _lock — see ApproveAsync's remark.
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return WithdrawCore(id, caller);
        }
        finally
        {
            _lock.Release();
        }
    }

    private ChangeRequestResult WithdrawCore(Guid id, string caller)
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
        // Waits on _lock, then holds it for the whole method below — including the store round trip
        // inside ChangeRequestPublisher.Apply. See the class remarks for why the whole span is one
        // critical section rather than two with a gap between them.
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

            // Every non-governed write still gets a real, traceable attribution: author and note come
            // straight from the caller. ApprovalRef is deliberately still set, to transient.Id — but
            // a direct write mints no ChangeRequest row (see the class remarks above: "nothing is
            // recorded"), so unlike ApplyValidatedAsync's ApprovalRef, this one names no request any
            // reader could ever look up. It is a correlation id only: stable for this one call, useful
            // for tying a version row back to the specific write attempt in a log line, but with no
            // backing record — not a claim that a governed request discharged this write.
            var provenance = new RuleChangeProvenance(
                transient.Author, transient.ChangeNote, ApprovalRef: transient.Id.ToString());

            return operation switch
            {
                DirectWriteOperation.RuleUpdate => OfRule(await _rules.PersistAndCommitCoreAsync(
                    change.Name, () => _rules.PrepareUpdateCore(change.Name, change.DocumentJson!, change.BaseVersion),
                    provenance, cancellationToken).ConfigureAwait(false)),
                DirectWriteOperation.RuleRevert => OfRule(await _rules.PersistAndCommitCoreAsync(
                    change.Name, () => _rules.PrepareRevertCore(change.Name, change.BaseVersion),
                    provenance, cancellationToken).ConfigureAwait(false)),
                DirectWriteOperation.PropositionCreate => OfProposition(await _propositions!.CreateCoreAsync(
                    change.Name, change.ModelTypeId!, change.DocumentJson!, change.Description, cancellationToken)
                    .ConfigureAwait(false)),
                DirectWriteOperation.PropositionUpdate => OfProposition(await _propositions!.UpdateCoreAsync(
                    change.Name, change.DocumentJson!, change.BaseVersion, cancellationToken).ConfigureAwait(false)),
                _ => OfProposition(await _propositions!.WithdrawCoreAsync(
                    change.Name, change.BaseVersion, cancellationToken).ConfigureAwait(false))
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

    // Despite the Core suffix, this always takes _listLock itself — every caller, whether or not it
    // holds _lock, needs a torn-free read of the list, and _listLock's critical sections are always
    // instantaneous, so acquiring it here costs nothing even from inside a _lock-held publish.
    private ChangeRequest? FindCore(Guid id)
    {
        lock (_listLock)
            return _changes.FirstOrDefault(change => change.Id == id);
    }

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
        /// <remarks>
        /// Holds both exclusion tiers, same shape as <see cref="RuleSet.PersistAndCommitCoreAsync"/>:
        /// the outer semaphore (<see cref="BindingScope.LockedAsync{T}"/>) serialises the whole publish
        /// await-safely, but <see cref="Validate"/> reads live scope state — <c>Scope.Graph</c> via
        /// <see cref="BindingScope.PrepareClosure"/> and <c>Referrers</c>, and <c>Scope.Source</c> via
        /// every <c>ValidateCore</c>/<c>PrepareCore</c> call — which is exactly the state a
        /// monitor-held rule commit (<see cref="RuleSet.PersistAndCommitCoreAsync"/>) or a
        /// monitor-held proposition write can be mutating concurrently. <see cref="Validate"/> is
        /// wholly synchronous, so wrapping it in <see cref="BindingScope.Locked{T}"/> costs nothing and
        /// gives it one consistent snapshot for its whole walk.
        /// <para>
        /// <see cref="ApplyValidatedAsync"/> is deliberately <em>not</em> wrapped here: it takes the
        /// monitor itself, twice — once to prepare the whole envelope, once to commit it — with the
        /// store round trips in between left unlocked, mirroring
        /// <see cref="RuleSet.PersistAndCommitCoreAsync"/> and <c>PropositionSet.PersistAndCommitCoreAsync</c>'s
        /// own shape: <see cref="BindingScope.Locked{T}"/> around prepare, the store <c>await</c>s
        /// outside it, then <see cref="BindingScope.Locked{T}"/> again around commit.
        /// </para>
        /// </remarks>
        public static Task<ChangeRequestResult> Apply(
            RuleSet rules, PropositionSet? propositions, ChangeRequest change,
            CancellationToken cancellationToken) =>
            rules.Scope.LockedAsync(
                async () => rules.Scope.Locked(() => Validate(rules, propositions, change))
                    ?? await ApplyValidatedAsync(rules, propositions, change, cancellationToken).ConfigureAwait(false),
                cancellationToken);

        /// <summary>
        /// Walks the envelope in exactly the order <see cref="Prepare"/> will, against a
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

            // Every node this envelope explicitly touches — computed once, up front, from the whole
            // change rather than incrementally: PrepareClosure needs to exclude a phase-C withdrawal
            // from a phase-A closure walk just as much as the reverse, and neither has happened yet
            // when phase A runs. See PrepareClosure's own remarks for why exclusion, not order, is
            // what keeps a node's own prepared change from being shadowed by a stale rebind of it.
            var envelopeNodes = EnvelopeNodes(change);

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
                if (rules.Scope.PrepareClosure(name, prospective, [], envelopeNodes) is { Count: > 0 } broken)
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
                    if (rules.Scope.PrepareClosure(name, prospective, [], envelopeNodes) is { Count: > 0 } broken)
                        return BrokenDependents(proposed.Target, broken);
                }

                withdrawn.Add(NodeId.Proposition(name));
            }

            return null;
        }

        /// <summary>
        /// Every node — rule or proposition — the envelope names, computed once up front so both
        /// <see cref="Validate"/> and <see cref="Prepare"/> can exclude the same complete set from
        /// every closure walk regardless of which phase runs first. Keyed by <see cref="NodeId"/>,
        /// not by bare name, for the same reason <see cref="Referrers"/>'s <c>rebound</c> dictionary
        /// is: a host may name a rule after a proposition, and merging the two would let one mask a
        /// genuine, distinct exclusion for the other.
        /// </summary>
        private static HashSet<NodeId> EnvelopeNodes(ChangeRequest change) =>
        [
            .. change.ProposedChanges.Select(proposed => proposed.Target.Kind == ChangeTargetKind.Rule
                ? NodeId.Rule(proposed.Target.Name)
                : NodeId.Proposition(proposed.Target.Name))
        ];

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
        /// Validate all → persist all → apply all. Everything fallible — every bind, both store round
        /// trips — runs before anything mutates, so an envelope cannot land half-way: the middle
        /// phase writes the whole rule half as one <see cref="RuleSet.AppendCoreAsync"/> batch and the
        /// whole proposition half as one <see cref="PropositionSet.WriteBatchCoreAsync"/> batch, and
        /// the final phase — applying every prepared change — is unable to fail.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The two store calls run rules-then-propositions. A crash between them leaves the rule rows
        /// durably written and the proposition rows not, but that is recoverable rather than
        /// corrupting: nothing has been <em>applied</em> either way — the rule rows are inert until
        /// the final commit phase runs, which never happens if the process dies first — and a rule row
        /// that no longer binds on the next <see cref="RuleSet.Load"/> is quarantined rather than
        /// fatal, which is precisely why quarantine exists. A process that does <em>not</em> crash but
        /// whose proposition store write throws is a related but distinct case — see
        /// <see cref="ChangeRequestOutcome.PersistenceDesynced"/>.
        /// </para>
        /// <para>
        /// A prepare failure here is treated as a bug (see <see cref="Unexpected"/>) with one
        /// exception, <c>VersionConflict</c>: <see cref="Validate"/> already ruled out every other
        /// refusal, but a version can still move between validation and this prepare — a background
        /// refresh (<see cref="RuleSet.Load"/>-shaped) mutates state under the inner monitor alone, not
        /// the outer gate this whole publish holds, so it can land in the gap between <see cref="Validate"/>'s
        /// own <see cref="BindingScope.Locked{T}"/> call and this one. That is an expected race, not an
        /// invariant breach, so it is reported as <see cref="ChangeRequestOutcome.VersionConflict"/>
        /// rather than thrown. A store refusing to append or write is the same distinction one layer
        /// down: expected (the store's own compare-and-set lost to a writer outside this process), not
        /// a bug, so it also comes back as a value.
        /// </para>
        /// </remarks>
        private static async Task<ChangeRequestResult> ApplyValidatedAsync(
            RuleSet rules, PropositionSet? propositions, ChangeRequest change,
            CancellationToken cancellationToken)
        {
            // --- Phase 1: prepare every rule and every proposition edit. Nothing is persisted or
            // applied yet — every bind that can fail has already run by the time this returns.
            var prepared = rules.Scope.Locked(() => Prepare(rules, propositions, change));
            if (prepared.Failure is { } prepareFailure)
                return prepareFailure;

            // --- Phase 2: persist the whole envelope as two independent all-or-nothing batches,
            // rules then propositions — see the remarks above for what a crash between them leaves.
            if (prepared.Rules.Count > 0)
            {
                var provenance = new RuleChangeProvenance(
                    change.Author, change.ChangeNote, ApprovalRef: change.Id.ToString());

                var appended = await rules
                    .AppendCoreAsync(prepared.Rules, provenance, cancellationToken)
                    .ConfigureAwait(false);

                if (appended.IsConflict)
                    return Failure(
                        ChangeRequestOutcome.VersionConflict, change,
                        new ChangeTarget(ChangeTargetKind.Rule, appended.Name!),
                        conflictVersion: appended.CurrentVersion);
            }

            if (prepared.PropositionWrites is { } batch)
            {
                try
                {
                    await propositions!.WriteBatchCoreAsync(batch, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // The rule row(s) above may already be durable (rules-then-propositions is
                    // deliberate — see the remarks), but nothing has been applied to live memory for
                    // either half: the apply phase below never runs. A bare retry of this same
                    // request would re-prepare against the *unchanged* live rule version and collide
                    // with the row this attempt already wrote, refusing forever — so this is a
                    // distinct outcome, not an ordinary conflict. See PersistenceDesynced's own
                    // remarks for what the caller must do instead of retrying.
                    // ToString(), not Message: an infrastructure fault is exactly when whoever reads
                    // this needs the exception's type and stack, not just its one-line message.
                    return new ChangeRequestResult(
                        ChangeRequestOutcome.PersistenceDesynced, change, null,
                        [new RuleError("$", RuleErrorCode.InvalidNode,
                            $"the proposition store failed after the rule half was already durably " +
                            $"persisted: {exception}")],
                        null, null, null);
                }
            }

            // --- Phase 3: apply every prepared change. Nothing below can fail.
            //
            // Propositions commit before rules here, but — unlike an earlier version of this method —
            // that ordering is no longer what keeps a rule's (or another proposition's) own explicit
            // publish safe from a co-edited node's stale dependent-closure rebind. That guarantee now
            // comes from Prepare's envelopeNodes exclusion set: PrepareClosure never builds a rebind
            // for a node the envelope also explicitly publishes or withdraws, in any phase, so no
            // commit here ever competes with another commit over the same node's live state — see
            // BindingScope.PrepareClosure's remarks. An ordering-only fix was tried and found
            // insufficient: a withdrawal's closure (phase C, structurally after every publish) can
            // reach a node a publish already committed, and no commit order satisfies both that case
            // and the reverse. Propositions-before-rules is kept here only because it happens to match
            // the order this method builds `prepared.PropositionPublishes`/`PropositionWithdrawals`/
            // `Rules` in — there is no correctness reason left to preserve it specifically.
            return rules.Scope.Locked(() =>
            {
                var versions = new Dictionary<string, int>(StringComparer.Ordinal);

                foreach (var (name, edit) in prepared.PropositionPublishes)
                    versions[name] = propositions!.CommitPublishCore(edit);

                foreach (var (name, edit) in prepared.PropositionWithdrawals)
                {
                    propositions!.CommitWithdrawCore(edit);

                    // No authored document remains, so there is no version left to report.
                    versions[name] = 0;
                }

                foreach (var (name, publication) in prepared.Rules)
                    versions[name] = rules.CommitCore(name, publication).Version;

                return new ChangeRequestResult(ChangeRequestOutcome.Ok, change, null, [], null, null, versions);
            });
        }

        /// <summary>
        /// Binds every edit in the envelope for real — producing publishable rule publications and
        /// proposition writes, not just errors — walking phases A/B/C in the same order
        /// <see cref="Validate"/> already proved would work. Assumes the scope lock is held.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Mirrors <see cref="Validate"/>'s prospective-overlay trick for the same reason: nothing
        /// commits to the live overlay until phase 3 of <see cref="ApplyValidatedAsync"/>, so a rule
        /// referencing a proposition prepared earlier in this same envelope — phase B referencing
        /// something phase A just prepared — must still resolve it, and only a prospective source
        /// carries that. <see cref="RuleSet.PrepareUpdateCore(string,string,int,ISpecSource)"/> and
        /// <see cref="PropositionSet.PrepareCreateCore"/>/<see cref="PropositionSet.PrepareUpdateCore(string,string,int,PropositionOverlay)"/>
        /// exist for exactly this.
        /// </para>
        /// <para>
        /// Phase C's withdrawal prepare deliberately skips re-deriving the direct-referrer check:
        /// <see cref="Validate"/> already ran it, accounting for referrers this envelope redirects
        /// away via its own <c>rebound</c> bookkeeping, under the same exclusive gate this whole
        /// publish holds — see <see cref="PropositionSet.PrepareWithdrawCore"/>'s remarks.
        /// </para>
        /// </remarks>
        private static EnvelopePrepare Prepare(RuleSet rules, PropositionSet? propositions, ChangeRequest change)
        {
            var prospective = new PropositionOverlay(rules.Scope.Overlay);
            var prospectiveSource = new LayeredSpecSource(prospective, rules.Scope.Registry);

            // See Validate's own remarks on this — the same complete-up-front set, so a closure walk
            // in any phase excludes every node this envelope also explicitly publishes or withdraws,
            // not only the ones prepared so far.
            var envelopeNodes = EnvelopeNodes(change);

            var ruleEdits = new List<(string Name, IRulePublication Publication)>();
            var publishes = new List<(string Name, PropositionSet.WritePrepare Edit)>();
            var withdrawals = new List<(string Name, PropositionSet.WritePrepare Edit)>();

            // Phase A — propositions coming into existence or changing.
            foreach (var proposed in Ordered(change, ChangeTargetKind.Proposition, deletions: false))
            {
                var name = proposed.Target.Name;

                // Unlike Validate — which binds both arms through one PrepareCore call and so has to
                // pick the model-type id and description itself — an update here goes through
                // PrepareUpdateCore, which reads both off the document it is replacing. Only a
                // creation needs them, and a creation has no stored state to take them from.
                var edit = propositions!.AuthoredStateCore(name).Exists
                    ? propositions.PrepareUpdateCore(
                        name, proposed.ProposedDocumentJson!, proposed.BaseVersion, prospective, envelopeNodes)
                    : propositions.PrepareCreateCore(
                        name, proposed.ModelTypeId!, proposed.ProposedDocumentJson!, proposed.Description,
                        prospective, envelopeNodes);

                if (edit.Failure is { } failure)
                    return FailWith(PropositionFailure(change, proposed.Target, failure));

                publishes.Add((name, edit));
            }

            // Phase B — rules, which may reference anything phase A just folded into the overlay.
            foreach (var proposed in change.ProposedChanges)
            {
                if (proposed.Target.Kind != ChangeTargetKind.Rule)
                    continue;

                var name = proposed.Target.Name;
                var result = proposed.ProposedDocumentJson is null
                    ? rules.PrepareRevertCore(name, proposed.BaseVersion, prospectiveSource)
                    : rules.PrepareUpdateCore(name, proposed.ProposedDocumentJson, proposed.BaseVersion, prospectiveSource);

                if (result.Publication is not { } publication)
                {
                    // A conflict here is an expected outcome, not a bug — see ApplyValidatedAsync's
                    // remarks. Reported through Failure(...); Mismatch(...) is the validation-phase path.
                    if (result.Outcome == RuleUpdateOutcome.VersionConflict)
                        return FailWith(Failure(
                            ChangeRequestOutcome.VersionConflict, change, proposed.Target,
                            conflictVersion: result.Version));

                    throw Unexpected(proposed.Target, result.Outcome.ToString(),
                        string.Join("; ", result.Errors));
                }

                ruleEdits.Add((name, publication));
            }

            // Phase C — propositions going away, which nothing may still resolve through by then.
            foreach (var proposed in Ordered(change, ChangeTargetKind.Proposition, deletions: true))
            {
                var name = proposed.Target.Name;
                var edit = propositions!.PrepareWithdrawCore(name, proposed.BaseVersion, prospective, envelopeNodes);

                if (edit.Failure is { } failure)
                    return FailWith(PropositionFailure(change, proposed.Target, failure));

                withdrawals.Add((name, edit));
            }

            return new EnvelopePrepare(null, ruleEdits, publishes, withdrawals);
        }

        /// <summary>
        /// Turns a proposition prepare's refusal into the envelope's result: a version conflict is an
        /// expected race (see <see cref="ApplyValidatedAsync"/>'s remarks), everything else is a bug
        /// <see cref="Validate"/> should already have ruled out.
        /// </summary>
        private static ChangeRequestResult PropositionFailure(
            ChangeRequest change, ChangeTarget target, PropositionUpdateResult failure) =>
            failure.Outcome == PropositionUpdateOutcome.VersionConflict
                ? Failure(ChangeRequestOutcome.VersionConflict, change, target, conflictVersion: failure.Version)
                : throw Unexpected(target, failure.Outcome.ToString(), Detail(failure));

        private static EnvelopePrepare FailWith(ChangeRequestResult failure) => new(failure, [], [], []);

        /// <summary>
        /// The outcome of <see cref="Prepare"/>: either the whole envelope's worth of publishable
        /// edits, or why one member refused to bind. A failure carries no edits, and edits come with
        /// no failure.
        /// </summary>
        /// <param name="Failure">Why one member refused to bind, or null when every one of them did.</param>
        /// <param name="Rules">Prepared rule publications, in phase B order.</param>
        /// <param name="PropositionPublishes">Prepared creates and updates, in phase A order.</param>
        /// <param name="PropositionWithdrawals">Prepared withdrawals, in phase C order.</param>
        private readonly record struct EnvelopePrepare(
            ChangeRequestResult? Failure,
            List<(string Name, IRulePublication Publication)> Rules,
            List<(string Name, PropositionSet.WritePrepare Edit)> PropositionPublishes,
            List<(string Name, PropositionSet.WritePrepare Edit)> PropositionWithdrawals)
        {
            /// <summary>
            /// The single store round trip the whole proposition half lands as, or null when the
            /// envelope touches no propositions and there is nothing to persist.
            /// </summary>
            public PropositionBatch? PropositionWrites =>
                PropositionPublishes.Count == 0 && PropositionWithdrawals.Count == 0
                    ? null
                    : new PropositionBatch(
                        [.. PropositionPublishes.Select(publish => PropositionSet.RowFor(publish.Edit.Authored!))],
                        [.. PropositionWithdrawals.Select(withdrawal => withdrawal.Name)]);
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
