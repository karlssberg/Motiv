# The decision log — record, retention, PII, and the write path

Type: grilling
Status: resolved
Blocked by: 09

## Question

Decided while charting: decision logging is **opt-in per rule, and total when on**. A rule marked
`audited` records *every* evaluation in full; others record nothing. Sampling is worthless for the
question that motivates the feature — "why was *this* customer declined?"

The payload already exists: `ResultSerializer.ToEvaluationResult(...)` produces a serialisable
`RuleEvaluationResult<string>` carrying the full justification, and `/api/checkout` builds two per
request today. This ticket is about *storing* what the code already constructs.

The session must resolve:

1. **What is a record?** The verdict, the justification tree, the rule version evaluated (a permanent
   reference — see 10.2), the timestamp, the caller identity, a correlation id — and **the input
   model**. That last one is the hard part.
2. **The input model is the PII problem.** Replay (fog) is impossible without the input; storing the
   input means storing whatever the adopter's model contains — names, ages, financial facts. Options:
   store it whole, store a hash, store an adopter-supplied redaction, or store a reference to the
   adopter's own record. This decision determines whether the feature is GDPR-tractable, and it is
   the reason this ticket cannot be deferred to the replay fog patch.
3. **Where does the flag live?** `audited` on the rule document (versioned with the rule, so the flag
   itself is auditable) or as separate configuration? The former is tidier and self-documenting.
4. **Off the hot path.** An audited rule on a checkout path must not pay a synchronous database write
   per evaluation. A channel plus a background writer is the obvious shape — but then a crash loses
   records, which for a compliance feature may be unacceptable. Bounded queue with what backpressure
   policy: block (protects evidence, risks the request path) or drop (protects latency, silently
   loses evidence)? **Whichever is chosen must be visible in the telemetry from ticket 04.**
5. **SDK or app?** The abstraction (`IDecisionSink`) in the SDK; the durable implementation in the
   app. Note this may be the same seam as the "emit, don't store" option — an adopter who wants their
   own log pipeline implements the sink.
6. **Retention.** Unlike version history, this is genuinely unbounded — an audited rule on a hot path
   is millions of rows. Retention policy is not optional here.

Feeds the fog patches "replay against historical versions" and "audit trail vs version history".

## Inherited from ticket 02

**The record must pin the build, not only the rule version.** A rule on a compiled default changes
behaviour when the code is redeployed with no version bump, and this is unfixable — `RuleDefault`
holds either a hashable document or a `SpecBase` built from C# delegates, and the latter has nothing
stable to fingerprint. So "rule version" alone does not identify behaviour for code-defined rules.
Record the build/assembly identity alongside it.

Consider also whether the `audited` flag should *require* a stored document, making the version
genuinely behaviour-identifying wherever it is being relied on for compliance.

## Inherited from ticket 09 — a constraint that does *not* apply

Ticket 09 settled the **authoring** store contract (outer `SemaphoreSlim`, async write path). **That
decision explicitly does not govern `IDecisionSink`.** Its reasoning — human-rate publishes, already
serialised by one gate, evaluation lock-free — fails entirely on the evaluation hot path. Decide
sub-question 4 (off-hot-path write, backpressure) on its own terms.

## Grounded in the code

- **The payload carries only the *outcome*.** `RuleEvaluationResult<string>` holds `Satisfied`,
  `Reason`, `Assertions`, `Values`, `Justification`, and an `Explanation` tree — **not** the input,
  version, caller, or timestamp. "Store what the code constructs" is true of the payload; the
  **envelope is entirely new**.
- **Nothing exists yet** — no `IDecisionSink`, no `audited`, no decision-log type. Designed fresh.
- `/api/checkout` (`Program.cs:117`) builds two `ToEvaluationResult`s per request today and discards
  them — this ticket stores them.

## Answer

**Opt-in per rule via an `audited` flag *on the rule document* (so it is versioned and governed). A
record pins behaviour with three anchors and captures input through an adopter-chosen seam — never a
silent whole-model default. Written off the hot path through `IDecisionSink`, `FailClosed` by default,
dropping only ever visibly. Stored raw-append in a separate database (16) under a mandatory retention
window.**

### Sub-3 first — the `audited` flag lives on the document, which discharges ticket 02 for free

`audited` is a field **on the rule document**, not separate config. Consequences, all good:
- It is **versioned** with the rule (10), so *when auditing was toggled* is in version history — a
  compliance fact ("was this rule audited at the time of that decision?").
- Toggling it is a **governed `ChangeRequest`** (13) — enabling/disabling audit is itself approved.
- **It forces "audited ⟹ stored document" by construction** — a rule on a compiled default has no
  document to hold the flag, so marking it audited transcribes its default into a stored, versioned
  document. Ticket 02's open question ("should audited *require* a stored document?") is answered as a
  *consequence of placement*, not a separate rule.

### Sub-1 — the record, and three provenance anchors

```
DecisionRecord(
  Id, CorrelationId, TimestampUtc, Caller,          // envelope (03, 04)
  RuleName, RuleVersion,                             // permanent version ref (10)
  BuildId,                                           // 02: compiled specs can't be fingerprinted
  ReferencedPropositionVersions,                     // 10: replay pin, a property of the evaluation
  Input?,                                            // sub-2 seam
  RuleEvaluationResult )                             // the existing payload
```

Reconstructing behaviour needs **three anchors, not one**: the **stored document** (the rule's own
composition — guaranteed present by sub-3), the **build id** (the compiled specs it references, ticket
02), and the **referenced proposition versions** (ticket 10 — a rule version does not pin what its
propositions said; this is where that pinning belongs, because it is a fact about the *evaluation*, not
the edit). Together: a complete behavioural fingerprint.

### Sub-2 — input capture is a seam; no silent whole-model default

The product cannot choose the privacy/replay tradeoff — it depends on the adopter's data and regime. So
input capture is a strategy on the sink path:

| Strategy | Replay | Privacy |
|---|---|---|
| `StoreWhole` | complete | stores raw PII — **dev only** |
| `Redact(projection)` | as far as the redaction preserved | adopter masks |
| `ReferenceOnly(keySelector)` | via the adopter's system-of-record | **GDPR-clean — recommended prod default** |

**Enabling `audited` requires choosing one** — a whole-model default that is on by omission is the
ticket-08 default-credentials trap applied to PII. **`ReferenceOnly` is recommended for production**
because it makes erasure and audit *coexist*: erase the subject in the adopter's SoR, the log keeps the
non-PII decision, and replay correctly becomes impossible. The strategy **sets the replay ceiling** —
the adopter trades privacy against replay fidelity, explicitly, per deployment. *(This is the seam that
graduates the "replay against historical versions" fog: replay = the three behaviour anchors × whatever
input the chosen strategy preserved.)*

### Sub-4 — off the hot path; `FailClosed` default; drop is never silent

A bounded channel + background writer feeding `IDecisionSink`. Backpressure is configured, with a
principled default:
- **`FailClosed` (default):** if an audited decision cannot be enqueued, the **decision itself fails** —
  "audited" means the record is load-bearing, so an audited decision that wasn't logged did not happen.
- **`Block`:** wait for capacity — protects evidence, risks request latency.
- **`Drop`:** shed load — **but never silently**: it writes a **gap-marker** into the log ("N records
  dropped here") and increments a **telemetry counter (ticket 04)**, turning a silent compliance hole
  into a provable gap.

The in-memory channel is a **bounded crash-loss window** (documented; keep the queue shallow so the
window is small). True zero-loss needs a durable queue (outbox/Kafka) — an adopter `IDecisionSink`
implementation, not the default. **All backpressure events must surface in ticket 04's telemetry.**

### Sub-5 — `IDecisionSink` in the SDK; durable writer in the app

SDK ships the interface, a default bounded-channel background-writer sink, and the input-capture
strategies. The app's implementation writes **raw-append to the separate decision-log database** (ticket
16 — not EF, machine-rate). "Emit, don't store" is the *same seam*: an adopter wanting their own
pipeline (SIEM, outbox, Kafka) implements the sink. Two-sidedness confirmed.

### Sub-6 — retention is mandatory; no unbounded default

Unlike version history (kept forever, ticket 10), this is genuinely unbounded (millions of rows), so a
**required, adopter-set retention window** with a background purge — no unbounded default. GDPR
minimization pushes it short; financial-audit regimes push it long (years). A record past the window
cannot be replayed, which is the **correct** post-retention state. Coupling to ticket 10: version-history
pruning (were it ever added) is bounded by this window, since decision records are what reference old
versions.

## Downstream

- **Graduates two fog patches:** "replay against historical versions" (= three anchors × captured input)
  and "audit trail vs version history" (this is the *decision* record of ticket 10's three-records model —
  distinct from the audit trail and version history, FK-referencing version history).
- **To ticket 04:** backpressure policy, drop-counters, and queue depth are required telemetry.
- **To ticket 16:** confirms the decision log is a separate database with a raw-append writer, and adds
  the retention purge job.
- **New machinery:** the `audited` document field, `DecisionRecord` + envelope, the input-capture
  strategies, the bounded-channel sink, and the retention purge.
