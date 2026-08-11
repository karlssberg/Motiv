# Rule version history and rollback — what is the record?

Type: grilling
Status: resolved
Blocked by: 02

## Question

`RuleSet.Update` carries optimistic concurrency: a stale `PUT` gets a `409` and the UI shows a
conflict banner with "Reload latest". So a **version** already exists — but only as a concurrency
token. Nothing retains what the previous version *said*. `Revert` restores the compiled default, not
the previous edit.

**What is a rule version, as a durable record?**

The session must resolve:

1. **What is stored per version?** The `RuleDocument` certainly. Also: who authored it, when, a
   change note ("why"), the approval that let it through (ticket 13), and the identity of the
   compiled default it descends from.
2. **Is the version number still just a concurrency token, or is it an identity?** If a decision log
   entry (ticket 15) references "the rule as it stood", that reference must be stable and permanent —
   which makes versions immutable rows, not a counter.
3. **What does rollback mean?** Restoring version N as a *new* version N+1 (append-only, audit-safe)
   or moving a pointer back to N (mutable head, cheaper, loses the fact that a rollback happened)?
   The append-only answer is almost certainly right for an audited product — confirm and record why.
4. **Retention.** Do versions live forever? A rule edited daily for five years is ~1,800 documents —
   trivial. This is probably a non-problem; say so explicitly rather than leaving it open.
5. **Does the same record serve the audit trail?** See the fog patch "audit trail vs version
   history". A version record answers *what the rule said*; an audit record answers *who did what,
   including reads, failed attempts, and permission denials*. They may share a table or deliberately
   not — this ticket should be the one that graduates that fog.

Blocks: 11 (draft/published split), 16 (reference implementation).

## Grounded in the code

- **Version is already a monotonic, forward-only sequence.** `RuleSet.Revert`
  (`Rules/RuleSet.cs:158`) *"moves forward, never back"* — reverting to the compiled default is a new
  forward version with a null document, not a decremented pointer. The append-only instinct is already
  the codebase's chosen semantics.
- **`RuleSetEntry` is a pure head row** — `(Name, Version, DocumentJson?)` plus type facts. `Update`
  overwrites the prior document; nothing retains what a previous version said. The ticket's premise
  holds exactly.
- **`IRuleStore` does not exist yet** (only `IPropositionStore` is in code). This ticket defines the
  record before ticket 16 builds its store — the right order.

## Answer

**Version history is an append-only log of immutable version rows — one per published change — each
carrying the document plus who/when/why. It is the stable *referent* that the audit trail and the
decision log both foreign-key into, and a *separate record* from both. Rollback appends. Versions are
kept forever. The model is symmetric across rules and propositions.**

### Sub-2 first — version is both an identity *and* a concurrency token

No tension between the two roles: a monotonic per-rule sequence where **each number names a permanent,
immutable row**, and the **maximum is the head**, which is what optimistic concurrency checks against.
The `int Version` already in `RuleUpdateResult`/`RuleSetEntry` keeps its concurrency meaning; ticket 10
only adds that the numbers below the head remain permanently addressable rather than being discarded.
Ticket 15's *"the rule as it stood at v5"* reference is therefore stable and permanent — the property
that forces immutable rows rather than a mutable counter.

### Sub-1 — what is stored per version

A version row: `(Name, Version, DocumentJson?, Author, TimestampUtc, ChangeNote?, ApprovalRef?, BuildId?)`.

- `DocumentJson?` — the `RuleDocument`; **null means "reverted to the compiled default at this point"**
  (inherits ticket 02's null semantics, now as a historical fact rather than only a head state).
- `Author` — the subject from the `ClaimsPrincipal` (ticket 03).
- `TimestampUtc`, `ChangeNote?` — when and why.
- `ApprovalRef?` — links to ticket 13's `ChangeRequest`; nullable, because dev-mode and pre-13 edits
  have no approval.
- `BuildId?` — for versions **on the compiled default**, a build stamp, *not* a content hash: the map
  already establishes delegates cannot be fingerprinted, so provenance of a compiled default can only
  be "which build", never "which content".

Two **judgment calls** (flag for override):
1. **`ChangeNote` is optional at the store level, required by the workflow.** Keep the SDK/store
   permissive; let ticket 13's publish workflow enforce "a note is mandatory to publish" if the adopter
   wants it. Baking the requirement into the store would break the dev-mode zero-config path.
2. **A version row does *not* pin the referenced proposition versions.** The document answers *what the
   rule said*; faithful *replay* needs the proposition versions in force at evaluation time, and that
   is a property of a recorded **decision**, not an authored change → it lives in ticket 15's decision
   log. Storing it on every version row would bloat history with data only replay uses. → note to 15.

### Sub-3 — rollback is append-only

Restoring version N writes a **new version N+1** whose document is a copy of N's, authored by whoever
rolled back, noted "restored from vN". Confirmed, and the reasons are concrete:

- **It records that a rollback happened.** A mutable-head rollback (move the pointer to N) erases the
  fact the head was ever at N+3 — the audit question "was this rule rolled back?" becomes unanswerable.
- **It is `Revert` generalised.** Revert-to-default already appends a forward version; rollback-to-vN
  is the identical operation with N's document as payload instead of null.
- **It keeps every version number permanently meaning one document**, so a decision-log reference to
  "v5" can never be invalidated by a later rollback reusing the number.

### Sub-4 — retention: forever, and say why it's a non-problem

**Versions live forever; no pruning in v1.** A rule edited daily for five years is ~1,800 small JSON
documents — storage is a non-issue, stated explicitly so it isn't left as an open worry. Any *future*
pruning is **governed by the decision log**: a version an entry references cannot be pruned, so
retention would become a function of decision-log references, never a free time-based purge. Additive,
deferred, not designed now.

### Sub-5 — the fog graduates: three records, version history is the spine

*"What did the rule say?"*, *"who did what?"*, and *"what did the rule decide?"* are **three distinct
records**, not one:

| Record | Answers | Shape | Keyed on |
|---|---|---|---|
| **Version history** (this ticket) | what the rule *said* | sparse, immutable, content-bearing — one row per published change | `(artefact, version)` |
| **Audit trail** (fog, graduated here) | who *did* what — incl. reads, 403s, stale 409s, failed binds, denials | dense, append-only, most rows mutate nothing | `(actor, time, action)` |
| **Decision log** (ticket 15) | what the rule *decided* per evaluation | machine-rate | `(evaluation, version-refs)` |

They must be **separate** because the cardinalities differ by orders of magnitude (a permission
*denial* has no version — nothing changed), the subjects differ (rule-keyed vs actor-keyed vs
evaluation-keyed), and the lifecycles differ (versions kept forever; audit may have a compliance purge
window). But audit and decision-log both **foreign-key into version history** — it is the stable spine
they point at. Version history denormalises a thin slice of its authoring event (`Author`,
`TimestampUtc`, `ApprovalRef`) so the common *"who last changed this rule and why"* question needs no
audit join. **Building the audit trail itself may be a later ticket; its *relationship* to version
history is settled here: separate record, FK into version history, never merged.**

### Symmetric across rules and propositions

Version history is a property of **authored artefacts**, not rules alone — ticket 02 made the stores
symmetric and ticket 12 unified their namespace, so propositions get the same version log. One
subtlety, load-bearing for replay: **a proposition version bump does *not* bump dependent rules'
versions** — the rules' documents did not change, only a proposition they reference did. This is
*exactly why* replay must pin proposition versions at the decision (sub-1 judgment call 2): a rule's
own version cannot encode the versions of what it composes.

## Downstream

- **To tickets 02 / 16:** ticket 02's head row stands as the **binding** record (`RuleSet` only ever
  binds the current document). Ticket 10 adds an **append-only version log** alongside it. Whether the
  head is a separate row or a projection of `max(version)` from the log is **ticket 16's** call. The
  version append is **atomic with the head update in one store transaction**, inside ticket 09's outer
  gate, in the established `bind → persist → commit` sequence — persist now writes two things (new
  version row + head), one transaction, one artefact's store (never crossing the rule/proposition
  store boundary ticket 02 drew).
- **To ticket 15:** proposition-version pinning for faithful replay lives in the decision log, not the
  version row.
- **Distinct from ticket 20:** the per-artefact `Version` sequence is *not* the store-wide monotonic
  generation / fencing token ticket 20 defines for multi-instance skew detection. Two different
  monotonic counters, different scopes — do not conflate.
- **Unblocks 11** (draft/published split builds on immutable versions) **and 16** (which now persists a
  log, not just a head row).
