# Motiv

Motiv turns boolean expressions into composable, explainable propositions (the Specification Pattern),
and — in the rules stack — lets those propositions be authored, versioned, and governed at runtime. This
glossary is the ubiquitous language for the **authoring and governance** domain. Core evaluation
mechanics (Spec/Policy builders, assertions, justification) are described in `CLAUDE.md` and `README.md`.

## Language

### Authoring

**Proposition**:
A named, reusable boolean question over a model (`customer.is-active`), authored as a building block for
others to compose. The atomic vocabulary of the rules stack.
_Avoid_: Predicate, condition, check

**Rule**:
A named, composed boolean decision over a model, built from propositions and specs and published for
evaluation (`pricing.eu.vat`). What an analyst authors and governs.
_Avoid_: Policy (reserved for Motiv's single-value result type), logic, condition

**Namespace**:
The dotted prefix of a rule or proposition name (`pricing.eu.*`), used as the unit of authorization and
organization. A pure projection of the names — there is no stored hierarchy to keep in sync.
_Avoid_: Folder, group, path

**Draft**:
A proposed change to a rule or proposition that is not yet live — authored and testable, but invisible to
evaluation until it is published. Mutable until published.
_Avoid_: Work-in-progress, unsaved, pending edit

**Published**:
The single live version of a rule that evaluation sees. Publishing is a distinct, governable act from
authoring a draft; with no gate configured the two fuse into save-is-publish.
_Avoid_: Live, active, released (as nouns for this state)

### Governance

**ChangeRequest**:
The governance envelope around a proposed change — the unit that is reviewed, approved, and published. It
carries one or more ProposedChanges, the author, a change note, and its accumulating approvals.
_Avoid_: PR, merge request, ticket, proposal

**ProposedChange**:
One artefact's proposed new state within a ChangeRequest (the target, the proposed document, and the
version it descends from). Several may travel together so that a rule and the proposition it needs publish
atomically.
_Avoid_: Diff, edit, patch

**Approval**:
A reviewer's positive assent to a ChangeRequest, recorded with approver identity and time. Approvals
accumulate; the gate counts them.
_Avoid_: Sign-off, vote, review (a review may also reject)

**Approval Gate**:
The rule that decides whether a ChangeRequest may publish — itself a Motiv single-value `may-publish`
Policy over the ChangeRequest, so a blocked publish explains itself through its own justification.
_Avoid_: Approval policy (ambiguous with Motiv's Policy type), workflow, ruleset

**Maker-checker**:
The segregation-of-duties property that a change's publisher must not be its author. Expressed as an
Approval Gate composition, not a separate permission.
_Avoid_: Four-eyes, dual control

**Break-glass**:
The deploy-time escape that disables the Approval Gate when it is misconfigured — loud and fully audited,
an infrastructure-layer act above any in-application permission.
_Avoid_: Override, bypass, admin mode

### Durability

**Version log**:
The append-only, immutable record of every rule publish — one row per `(Name, Version)`, kept forever
and never rewritten. The primary key is also the cross-process compare-and-set: two replicas racing
the same version race on the insert, and the key lets exactly one win.
_Avoid_: History table, audit log (the log *is* the audit trail, not a copy of one), changelog

**Head projection**:
A rule's current state as a store reports it — the highest-versioned row in that rule's version log,
reduced to name, version, and document. Never stored separately; always derived, so it cannot drift
from the log it summarizes.
_Avoid_: Snapshot, current row, latest version (as a stored thing rather than a computed one)

**Quarantine (rule)**:
The non-fatal state of a stored rule document that no longer binds after a load — a redeploy renamed
a spec it referenced, say. The rule keeps evaluating its compiled default, its stored version is
preserved for repair, and the reason is reported rather than silently swallowed or fatal at startup.
Mirrors proposition quarantine.
_Avoid_: Broken, corrupted, disabled

**Generation**:
A monotonically increasing, store-wide counter that moves whenever any write lands — a scalar fencing
token a replica can poll to tell whether it is behind without re-reading the whole store.
_Avoid_: Version (reserved for one rule's own counter), revision, sequence number

**Provenance**:
The who/why attached to a publish — author, an optional change note, the change request it discharged
when governed, and the build that was live — carried as one value and written into the version log
with every row.
_Avoid_: Metadata (too generic), audit fields, attribution
