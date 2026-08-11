# Cross-process write coordination — moving optimistic concurrency into the store

Type: grilling
Status: resolved
Blocked by: 20

## Question

Deferred from ticket 20, which chose "refresh now, coordination later". Refresh makes replicas
*converge*; it does nothing to stop them *conflicting*.

### The concrete lost update

The version compare-and-swap is `Interlocked.CompareExchange` on a field. It protects concurrent
writes within one process and knows nothing about the other replica.

| | replica A | replica B |
|---|---|---|
| both hold rule X at v5 | | |
| Analyst 1 → A, `baseVersion: 5` | CAS sees 5 ✓ → writes **v6** | |
| Analyst 2 → B, `baseVersion: 5` | | CAS sees 5 ✓ → writes **v6** |

Analyst 2's write lands on top of Analyst 1's. **Both received `200 OK`.** Neither saw the `409` the
system exists to produce. Analyst 1's change is gone, and the audit trail records two publishes both
claiming v6 — which is worse than losing the edit, because the record is now wrong.

The window is bounded by the refresh interval, so two analysts must touch the same rule within it.
Rare. Not impossible. Silent.

## The session must resolve

1. **Is a conditional store write the mechanism?**
   `UPDATE rules SET document = ?, version = 6 WHERE name = ? AND version = 5`, then check
   rows-affected. Trivial SQL — but it changes `IRuleStore.Save` from *a sink that cannot fail* into
   *something that can return a conflict*. That contradicts a stated invariant: **"A store is a dumb
   sink — it validates nothing and enforces no invariants. Legality is decided by `PropositionSet`
   before anything reaches here."** It is also the premise ticket 09 relied on to conclude that
   everything fallible runs before anything that mutates.
2. **What happens to the ordering discipline?** Today: bind → check dependents → persist → mutate
   memory → commit, where the store is last-of-the-fallible. A store that can *reject* on version
   conflict is still fallible-before-mutation, so the ordering may survive intact — **confirm this**,
   because if it does, the change is far smaller than it first appears.
3. **Does the in-memory CAS stay?** Belt and braces (fast local rejection, store as the authority), or
   remove it as a now-misleading duplicate? Keeping two authorities for one invariant is how they
   drift.
4. **Does this reopen ticket 02's record?** `StoredRule(Name, Version, DocumentJson?)` is unchanged in
   shape, but `Save`'s *contract* changes. Establish whether the record survives and only the method
   signature moves.
5. **Propositions too?** `PropositionSet` has the same exposure via its own version checks. One
   mechanism or two — bearing in mind ticket 02 concluded the two stores are never written in the same
   transaction, so they can be coordinated independently.
6. **Is there a cheaper answer?** A store-held write lease (one replica is the writer at a time) avoids
   per-row conditional writes entirely and may suit a governance product with a low write rate. Weigh
   it before assuming conditional updates are the only option.

Inherits from ticket 20: the store already grows a monotonic generation for refresh triggering and
skew detection. Whether that same sequence can serve write coordination is worth checking — it may
already be most of the mechanism.

## Grounded in the code

- The in-memory CAS is `Interlocked.CompareExchange(ref _state, replacement, expected)` at
  `Rule.cs:170` and `AsyncRule.cs:175` — an in-process field swap, blind to the other replica exactly
  as charted.
- Ticket 09's outer `SemaphoreSlim` gate is a resolved *decision*, not yet code; the target state below
  assumes it (and ticket 16's schema).

## Answer

**Ticket 16 already built the mechanism, for an unrelated reason. Ticket 21 is a deletion plus a
mapping, not a coordination subsystem: remove the in-memory CAS, let the version log's `(Name, Version)`
primary key be the cross-process compare-and-set, and surface its violation as the existing
`VersionConflict`. Optimistic, not a lease; the same pattern for propositions.**

### The key finding — the fix is a side effect of ticket 16

Ticket 16 chose **head-as-projection** (no mutable head row; current version = `max(RuleVersion.Version)`)
to make head/log divergence unrepresentable — a durability decision. Its consequence: publishing v6 is
an `INSERT RuleVersion(Name, 6, …)`, and the `(Name, Version)` PK — which 16 kept as a structural
constraint and flagged as "doubles as the cross-replica append guard" — **is** the compare-and-set. Two
replicas at v5 both compute next=6, both `INSERT (Name, 6)`; the PK lets one win and rejects the other.
The lost update the ticket describes (two publishes both claiming v6) becomes *impossible* — and the
audit trail is now correct: one published v6, one rejected attempt (ticket 10's failed-attempt event).

A stale replica cannot slip through: holding v5, it can only ever compute next=6, so it collides on the
PK. To compute 7 it must first have refreshed to see v6 — at which point it is legitimately building on
v6, not losing an update. So the guard is airtight for the lost-update case. Read staleness is ticket
20's concern (refresh + fencing token); write safety is this PK. They compose cleanly: **20 handles read
convergence, 21 handles write conflict, on the same atomic append.**

### Sub-1 — the dumb-sink tension dissolves (no carve-out needed)

The mechanism is a structural constraint, not store validation logic: the *database's* PK rejects the
duplicate; the store merely surfaces it. Ticket 16 already ruled identity/structural constraints (the
PK) KEPT precisely because they are **not semantic legality**. So "the store validates nothing semantic"
stays literally true — concurrency control is delivered by a constraint the dumb-sink principle already
permits. The apparent contradiction the ticket raises is answered by a decision already on the map.

### Sub-2 — the ordering survives intact (confirmed)

The conflict surfaces at the `INSERT` (persist), still **before** mutate-memory → commit. It is one more
fallible step at the persist point, and everything fallible already runs before mutation (ticket 09's
premise), so a rejected write leaves nothing live behind it. Nothing reorders; a failure mode is added
to a step already in the fallible-prefix. This is why the change is far smaller than it first appears.

### Sub-3 — remove the in-memory CAS

It is **redundant intra-process** (09's gate serializes writes, so two in-process writes never race the
CAS) and **insufficient inter-process** (blind to the other replica) — it protects nothing the gate does
not and fails at the case that matters, the exact "two authorities drift" hazard the ticket names.
Remove it. In-memory state keeps the version *value* (for the projection); *enforcement* moves to the
PK. Delete `Rule.cs:170` / `AsyncRule.cs:175`'s CAS.

### Sub-4 — record unchanged; only the `Save` contract moves

`StoredRule(Name, Version, DocumentJson?)` is untouched. `SaveAsync` gains a conflict return — the
**already-existing `RuleUpdateResult.VersionConflict`**, produced by the PK violation instead of the CAS.
One outcome relocates from CAS-produced to store-produced; nothing new is invented. Composes with ticket
09's async signature change (the return type was already moving).

### Sub-5 — one pattern, two independent applications

Rules and propositions both get PK-guarded appends. **No shared coordinator** — ticket 02 established the
two stores are never written in the same transaction, so they coordinate independently. Symmetric with
10/11/12's rule↔proposition treatment.

### Sub-6 — optimistic wins; the lease is rejected

The conditional write is nearly free (it is the PK, already present). A **write-lease** adds a
single-writer bottleneck plus acquire/renew/expire/failover machinery — a coordination subsystem to
replace something already free — for a low-write-rate governance product where conflicts are rare (so
optimistic's "rebase on conflict" cost is seldom paid). Rejected.

On the **ticket 20 generation**: complementary, not the conflict key. Store-wide generation serves
*skew/refresh* (20); the per-rule version serves *conflict* (21); both bump in the same atomic append.
The ticket's hunch is half-right — the per-write atomic-counter infrastructure exists, but the conflict
key must be per-rule (the version), not store-wide (the generation), or concurrent writes to *different*
rules would falsely conflict.

## Downstream

- **Closes the Durability & data bundle** (02, 09, 10, 16, 20, 21) and the last architecturally-open
  ticket on the map.
- **To ticket 16:** map the `(Name, Version)` PK unique-violation to the `VersionConflict` outcome in the
  reference store; this is the concurrency behaviour, no new column.
- **To ticket 09:** `SaveAsync` returns a conflict outcome; folds into its already-changing signature.
- **To ticket 10:** a rejected publish is a failed-attempt audit event — which is what makes the audit
  trail *correct* (one v6, one rejected) where today it would record two v6 publishes.
