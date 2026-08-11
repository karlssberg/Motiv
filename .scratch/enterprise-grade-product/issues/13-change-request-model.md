# The `ChangeRequest` model and its built-in approval specs

Type: grilling
Status: resolved
Blocked by: 11, 12

## Question

Decided while charting: the approval gate is **admin-configurable**, and it is expressed as **a Motiv
rule over a `ChangeRequest`** rather than as bespoke configuration. The product governs itself with
its own engine, and a blocked publish explains itself through `Justification`.

Consult `/domain-modeling` — this ticket introduces the domain's most important new noun.

**What is a `ChangeRequest`, and which specs ship built-in?**

The session must resolve:

1. **The model's shape.** Candidate fields: the target (rule or proposition name, and therefore its
   namespace), the proposed document, the version it descends from, the author, the change note, the
   set of approvals so far (each with approver identity and timestamp), whether it is a rollback,
   whether it is a creation or a deletion, and the diff's shape (did it change structure, or only a
   metadata string?).
2. **The built-in spec catalogue.** These are what admins compose with, so they *are* the
   configuration surface. Candidates: `change.in-namespace(prefix)`,
   `change.approver-count-at-least(n)`, `change.author-is-approver`, `change.is-rollback`,
   `change.is-deletion`, `change.approver-has-role(role)`, `change.touches-async-spec`,
   `change.is-metadata-only`. Which of these need **parameters** — and does
   `RuleParameterResolver` already support the shapes required, or does this push on it?
3. **Which direction does the rule read?** Does a satisfied rule mean *may publish* or *requires
   review*? Motiv's explainability is strongest on refusal, so `may-publish` with an unsatisfied
   result carrying the reasons is probably right — confirm and record why.
4. **Where does the gate live — SDK or app?** The two-sidedness rule says the `ChangeRequest` model
   and specs belong in the SDK (an adopter embedding the SDK wants the gate too), and only the
   *stored configuration* is app-side. Confirm.
5. **What is the default rule?** Shipped out of the box, before any admin configures anything. A
   permissive default preserves the demo's hot-swap story; a strict default is safer. See 11.6.
6. **Does the same gate cover propositions and spec registration?** An engineer publishing a
   proposition changes what every rule referencing it means — arguably a *higher*-stakes change than
   editing a rule.

Blocks: 14 (bootstrapping and lockout).

## Grounded in the code

- **`RuleParameterResolver` supports only scalar parameter types** (`Integer`, `Number`, `String`,
  `Boolean` — `RuleParameterResolver.cs:84-92`). Every candidate built-in spec's parameter is a scalar
  (`prefix: string`, `n: int`, `role: string`) or nullary, so the `ChangeRequest` catalogue **does not
  push on the resolver.** No collection/object parameter is required.
- **No `CONTEXT.md` / `docs/adr/` exists yet** — the domain model is captured here (plan-only); a real
  glossary entry + ADR are *offered* for promotion, not created unilaterally.

## Answer (domain-modeling applied)

**A `ChangeRequest` is the governance envelope around one or more proposed changes; the approval gate is
a Motiv `may-publish` Policy over it, so a blocked publish explains itself through `Justification`. The
default gate is permissive (lockout-safe, preserves hot-swap); it covers propositions but not
spec-registration; the model + specs are SDK, the active-gate config is app.**

### The central sharpening — `ChangeRequest` is an envelope, not a draft

Stress-tested with the coordinated-change scenario the map kept deferring here: *add proposition
`geo.in-eu` **and** edit rule `pricing.eu.vat` to use it — they must publish together or the rule
references a proposition that isn't live.* Unexpressible if a `ChangeRequest` is 1:1 with one draft
document. Therefore:

- **`ChangeRequest`** = the governance envelope. Contains **one or more `ProposedChange`s**, carries the
  author, change note, approvals, and workflow status; **publish is atomic across all its changes.**
- **`ProposedChange`** = one artefact's proposed new state: `(Target, ProposedDocument?, BaseVersion,
  Classification)`.
- **Ticket 11's `Draft` was the implementation name for a single `ProposedChange`.** One domain noun,
  not two — this reconciles 11 and 16's provisional `Draft`/`ChangeRequest` tables into one envelope.
  v1 may *create* mostly single-change requests, but the model is 1:many so the coordinated case is not
  a later migration of the core noun.

### Two term-sharpenings

- **Approval vs Rejection are asymmetric.** `Approvals` is an accumulating set of positive assents
  `(Approver, TimestampUtc)` that the gate counts; a **Rejection** is a terminal transition with a
  reason, not another approval row.
- **Two distinct lifecycles.** `ChangeRequest.Status` (`Draft → InReview → Approved → Published`, with
  `Rejected`/`Withdrawn` terminals) is the *workflow*; it is **not** the version-row status from ticket
  11 (`Draft/Published/Superseded`). A `ChangeRequest` reaching `Published` *produces* a published
  version row. Different entities, different lifecycles — kept apart deliberately.

### Sub-1 — the model, with a derive-vs-store rule

```
ChangeRequest(
  Id,
  Author,                      // ClaimsPrincipal subject (03)
  ChangeNote,                  // the "why" (10; workflow-required)
  Approvals: [(Approver, TimestampUtc)],
  Status,                      // workflow lifecycle above
  ProposedChanges: [ ProposedChange(
      Target: (Kind: Rule|Proposition, Name, Namespace),
      ProposedDocument?,       // null = deletion/revert to compiled default (02/10)
      BaseVersion,             // optimistic concurrency (11) — 409 at publish if stale
      Classification) ] )
```

**Classification is mostly *derived*, not stored** — a pure function of `(ProposedDocument, base
version's document, target)`, so storing it invites drift (same lesson as ticket 16's head/log
projection):
- **Derived:** `IsCreation` (no base version), `IsDeletion` (null doc), `IsMetadataOnly` (structural
  diff empty — only metadata strings changed), `TouchesAsyncSpec` (from referenced specs).
- **Stored intent** (what the diff cannot recover): `IsRollback` + source version — "rolled back to v5"
  and "coincidentally authored a doc identical to v5" share a diff but are different governance events.

### Sub-2 — the built-in catalogue (the configuration surface)

All scalar-parameterised or nullary, so `RuleParameterResolver` covers them:

| Spec | Param | Reads |
|---|---|---|
| `change.in-namespace(prefix)` | `string` | target namespace (12's prefix tree) |
| `change.target-is-proposition` | — | target kind (sub-6 higher-stakes gating) |
| `change.approver-count-at-least(n)` | `int` | `Approvals.Count` |
| `change.author-is-approver` | — | self-approval — usually negated in a gate |
| `change.approver-has-role(role)` | `string` | an approval from a role-holder |
| `change.is-rollback` | — | stored intent |
| `change.is-deletion` / `change.is-creation` | — | derived structural |
| `change.is-metadata-only` | — | **derived diff-shape — needs a new document structural diff** |
| `change.touches-async-spec` | — | derived from referenced specs |

**Maker-checker lands here concretely** — it is `change.approver-count-at-least(1) & !change.author-is-approver`,
which is exactly ticket 12's "maker-checker is a *workflow*, not a grant". Admins compose these into a
`may-publish` policy, e.g. `change.in-namespace("pricing") ⟹ (change.approver-count-at-least(2) & !change.author-is-approver)`.

**One new-machinery flag:** `change.is-metadata-only` needs a **structural diff of two rule documents**,
which does not exist today. It is the one built-in that isn't a trivial predicate — worth it (a typo-fix
in an assertion string deserves a lighter gate than a logic change), and the implementation cost this
ticket carries.

### Sub-3 — the gate reads `may-publish`; refusal explains itself

A Motiv **Policy** over `ChangeRequest` where **satisfied = may publish**. An *unsatisfied* result
blocks the publish and its `Justification` names precisely the unmet conditions. `may-publish` (not
`requires-review`) is right because Motiv's de-noising surfaces the causal assertions of the outcome —
so putting the block on the *unsatisfied* path makes the refusal information-rich, which is the whole
product aesthetic. A Policy (single value), not a Spec, because "may I publish" is one decision.

### Sub-5 — default gate is permissive (and lockout-safe)

Shipped default: `may-publish` always satisfied — no approval required. This preserves ticket 11's
hot-swap story out of the box, and it is the **only lockout-safe bootstrap**: a strict default could
refuse the very publish that installs the first real gate, before any approver exists (→ ticket 14). It
is *not* "anyone can publish": authentication and the `publish` grant still apply (03/12); only the
*additional approval ceremony* is opt-in. Access locked, workflow ceremony opt-in — the coherent
default.

### Sub-6 — covers propositions, not spec-registration

- **Propositions: yes**, and they are *higher*-stakes (a proposition change alters what every
  referencing rule means), so an admin can gate them *stricter* via `change.target-is-proposition`.
  Consistent with 10/11/12's rule↔proposition symmetry.
- **Spec-registration: no** — ticket 12 established `register-spec` is a compile/deploy-time act, not a
  runtime authoring action, so it never becomes a `ChangeRequest`. This corrects sub-6's implication.

### Sub-4 — SDK owns model + specs; app owns the active-gate config

The `ChangeRequest` model, `ProposedChange`, and the built-in spec catalogue live in the SDK (an
adopter embedding the SDK wants the gate). Only the **stored active-gate configuration** (which composed
rule is the gate, per namespace) is app-side. The gate being *itself a Motiv rule* means it is stored
and versioned like any governed rule — which raises "who approves a change to the gate?", a genuine
lockout hazard handed to **ticket 14**.

## Downstream

- **To ticket 14 (bootstrapping/lockout):** the gate governs itself (a gate change is a `ChangeRequest`
  the gate must pass) and the permissive default is the safe seed — 14 owns the lockout resolution.
- **New machinery this ticket introduces:** a document **structural diff** for `change.is-metadata-only`.
- **To ticket 16:** the provisional `Draft`/`ChangeRequest` tables collapse into one `ChangeRequest`
  envelope with a child `ProposedChange` row per artefact (mutable until published; publish transcribes
  each into an immutable version row).
- **Offered, not created (plan-only):** a `CONTEXT.md` glossary entry for `ChangeRequest` /
  `ProposedChange` / `Approval` / gate, and an ADR for "the approval gate is a Motiv rule over
  ChangeRequest (the product governs its own changes)" — which meets all three ADR criteria
  (hard to reverse, surprising, a real trade-off vs a bespoke config DSL).
