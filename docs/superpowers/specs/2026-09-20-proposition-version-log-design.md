# Proposition version log — Design

**Date:** 2026-09-20
**Status:** Implemented
**Parent:** `2026-09-20-decision-reproduction-mcp-design.md`, section 2. Slice 1 of 5. Resolves #224.
**Plan:** `docs/superpowers/plans/2026-09-20-proposition-version-log.md`

## Problem

The decision log pins the version of every proposition a rule resolved through, but the
proposition store replaced rows in place, so that number named a document nobody kept. The
2026-09-09 CAS design closed the concurrency half of the rule/proposition asymmetry and
deliberately left history open, asking whether replay was an obligation. The reproduction design
answers yes: a logged decision must be re-runnable against the documents that decided it.

## Decisions

1. **The `WriteAsync(PropositionBatch)` contract is kept.** The CAS design said a log could
   implement it "by a PK insert with no caller change". `HistoryAsync(name)` is the only addition
   to the interface.
2. **A deletion writes a tombstone at `version + 1`.** `Load()` excludes it; the history keeps it,
   with the retired row's model type carried onto it. Re-creation continues past it, so a version
   number is never reused for a name. `PropositionBatch.FindConflict` now takes a
   `PropositionPosition` (highest version, live or not) rather than a bare version: a save must
   land strictly past the highest row, tombstones included; a deletion must name the live head.
3. **Provenance travels on the batch.** Every row carries author, note, approval reference and
   build id. Direct writes are attributed to the request's principal, governed writes to the
   change request (author, note, and the request id as `ApprovalRef`), and callers that pass
   nothing to `system`. The rule and proposition halves of an envelope share one provenance value.
4. **The store stamps the timestamp.** A rule row is stamped by `RuleSet`; a proposition row by
   the store, through `StoredPropositionVersion.Saved` / `Tombstone`. One place rather than two,
   since two callers (`PropositionSet` and `ChangeRequestSet`) build proposition batches.
5. **`PropositionSet` reads the history once per creation** to learn the next version. Under the
   monitor it cannot await, so the read precedes the locked prepare; the governed envelope reads
   every creation's next version before its locked `Prepare` for the same reason. A racing replica
   computes the same number and is refused by the key.
6. **`MotivPropositionVersion` replaces `MotivProposition`.** The concurrency token goes with it:
   the `(Name, Version)` primary key is the compare-and-set, exactly as for rules, and the head is
   projected in SQL. No migration: the schema guard names the missing table and says what to do.
7. **Studio's JSON store is a log too**, with an atomic rewrite, and it still reads a file written
   before provenance existed (rows lacking `author` become system-authored version rows).
8. **Import carries heads, not history.** The JSON store predates the log; imported rows are
   attributed to the system, and a source with a tombstoned head imports nothing for that name.
9. **Rows are kept forever.** Retention, if ever configured, is bounded by the decision log's own.

## Rejected

- **A separate head table beside the log.** Two writes per publish, and a head that can drift from
  the log it is meant to summarise. Rules derive the head in SQL; propositions now do too.
- **Tombstones in `Load()` as null-document heads.** A null document is already the malformed-row
  case `PropositionSet` quarantines, and overloading it would put withdrawn names in the catalog
  as broken.
- **Restarting a re-created name at version 1.** The decision log may pin the old v1.
- **A `Sequence`/incarnation key that lets version numbers repeat.** `PropositionVersion(Name,
  Version)` on the decision record would then be ambiguous.

## Outcome

Every store passes the shared `PropositionStoreConformance` suite, now 27 tests, including the
tombstone, re-creation, provenance and refused-batch cases. The full solution suite is green on
net10 (7,820 tests across thirteen projects) and `Motiv.Serialization.Tests` also on net8 and
net9; net472 builds and is exercised by CI. One test that had proved the EF concurrency-token
mechanism was rewritten to prove the primary-key mechanism that replaces it.
