# The draft/published split — replacing save-is-publish

Type: grilling
Status: resolved
Blocked by: 10

## Question

Today **saving is publishing**. A `PUT` validates, binds, and hot-swaps the live rule atomically, and
the demo's pitch is exactly that: *"save a rule change and the very next checkout reflects it."*
That property is genuinely good, and an approval gate destroys it unless the split is designed
deliberately.

**What replaces save-is-publish, and what is preserved of it?**

The session must resolve:

1. **What states exist?** Draft / published is the minimum. Does an *approved-but-not-yet-published*
   state exist, or does approval publish immediately? The latter is simpler; the former is what
   enables scheduled or coordinated releases.
2. **Where do drafts live?** In the store as versions with a status, or in a separate space? Note
   this interacts with 10's append-only answer: if versions are immutable, a draft is just a version
   that was never published, which is tidy.
3. **What does the *live* path see?** `RuleSet` must continue to serve only published rules to
   evaluation, with the same snapshot guarantee — a concurrent draft save must never tear a result.
4. **Can a draft be evaluated?** It must be, or authors cannot test before requesting approval.
   `POST /evaluate` already evaluates an arbitrary document without publishing — that is the existing
   answer and it may need nothing more. Confirm.
5. **Does the hot-swap pitch survive?** Consider whether an admin can configure a namespace to
   require no approval, in which case save-is-publish remains available as a *configuration* rather
   than being deleted. This connects directly to 13's configurable gate — the demo's story could
   become "no approval configured" rather than "a different product".
6. **Concurrent drafts.** Two analysts editing the same rule: one draft per rule, or one per author?
   The `409` model assumes a single head.

Blocks: 13 (the `ChangeRequest` model).

## Grounded in the code

- **A draft already exists — client-side only.** `Rule.cs:149` / `PropositionSet.cs:675` reason about
  *"an editor's open draft"*, and today `PUT` = validate + bind + compare-and-swap = *publish*. The
  draft is whatever the browser holds; nothing durable. **This ticket promotes the draft from a
  client-side unsaved edit to a durable server-side state.**
- **The live path is a compare-and-swap** (`RuleSet.cs:136` — *"the live rule is untouched unless the
  document binds and the expected version holds"*), serialized against direct state changes by ticket
  09's gate. Publishing keeps exactly this swap; the split only decides *what* triggers it.

## Answer

**The split *factors* save-is-publish, it does not replace it. `PUT` today does two atomic things —
persist the edit, swap the live rule; the split names them `author` and `publish` so a gate can slot
between. With no gate configured they re-fuse into today's behaviour, so save-is-publish is the
*degenerate case of the general model*, and the dev-mode default. Drafts are mutable, in their own
table; only publish mints an immutable version; the live path never sees a draft by construction.**

### Sub-2 (settles the rest) — drafts are mutable staging, not immutable versions

A refinement to ticket 10's hint (*"a draft is just a version that was never published — tidy"*): tidy
as a **mental model**, but operationally **drafts are mutable and live in a separate `Draft` table;
only *publish* mints an immutable `RuleVersion` row.** If every keystroke-save were an immutable
version, the log bloats and version numbers gap (drafts consuming numbers never published). Ticket 10's
immutability exists to make *what was live* audit-stable — and a draft was never live, so freezing it
serves no audit purpose. Bonus: ticket 16's projection (`current published = max(RuleVersion.Version)`)
needs **no status filter**, because drafts are not in that table at all. Confirms ticket 16's
provisional `Draft` table.

### Sub-1 — states: a status enum with room to grow; v1 uses two

Durable status: `Draft`, `Published`, `Superseded`, with `Approved` / `Rejected` **reserved**. v1
*operates* two-state — Draft → Published, approval publishing immediately — consistent with ticket 12's
"approve folds into publish". The `Approved`-but-not-yet-published intermediate (scheduled / coordinated
release) is a **ticket 13** activation of a status the model already permits. Future-proof schema, no
unused workflow built now.

### Sub-3 — the live path is protected by construction

Drafts live in the `Draft` table and are **never bound into `BindingScope`**, so evaluation *cannot*
see a draft — binding only ever consumes published versions. A concurrent draft save cannot tear a
result because it never enters the evaluation path; it writes a table the live path never reads. Publish
is the sole rebind and keeps the existing atomic compare-and-swap (ticket 20's whole-overlay
copy-and-swap). The snapshot guarantee is preserved with no new machinery.

### Sub-4 — a draft is evaluated via `/evaluate`; one boundary

Confirmed: `/evaluate` on an arbitrary document (ticket 03) *is* the draft-test mechanism; nothing more
needed. **Boundary:** it resolves referenced propositions against **published** state, so testing a
draft rule against *simultaneously-draft* propositions is a coordinated-change scenario owned by ticket
13's `ChangeRequest` (which bundles several drafts). → note to 13.

### Sub-5 — the hot-swap pitch survives, as a configuration

With no approval gate configured (ticket 13), a user holding both `author` and `publish` grants
saves-and-publishes in one motion → save-is-publish, verbatim, with the same atomic immediate swap.
**This is the dev-mode default** (single superuser, no gate — tickets 08 / 12). The split only
*manifests* when governance is switched on, and adds latency between draft and publish only then. The
demo's story becomes *"no approval configured"*, not *"a different product"* — and it is preserved as
the general model's degenerate case, not kept alive as a special path.

### Sub-6 — many concurrent drafts, single published head

One-draft-per-rule forces out-of-band coordination — exactly what a workflow should remove. So:
**multiple concurrent drafts are allowed against the same artefact, keyed per proposed change** (ticket
13 names the change identity), while there remains **exactly one published head**. The existing
`409` / `baseVersion` optimistic concurrency resolves conflicts **at publish time**: a change drafted
against v5 that publishes after another already reached v6 gets a `409` — *"your base is stale, rebase
your change."* Published history stays strictly linear; drafts never touch `RuleVersion`, so the
projection stays clean. In dev mode (one author, no gate) this degrades gracefully to one draft at a
time.

## Downstream

- **To ticket 13:** owns the `ChangeRequest` that is a draft's identity/owner; the configurable gate
  that decides whether publish is immediate or held; coordinated multi-draft changes (draft rule +
  draft proposition together); and activation of the reserved `Approved` state for scheduled release.
- **To ticket 16:** the provisional `Draft` table is confirmed and mutable, keyed per-change
  `(ArtefactName, ChangeId)`, holding the in-progress document + author + `baseVersion` + change note.
  On publish it is transcribed into the next immutable `RuleVersion`/`PropositionVersion` row and closed.
  Polymorphic over rules and propositions (ticket 10/12 symmetry).
- **To ticket 10:** refines its sub-2 hint — a draft is *not* an immutable version row; immutability is
  a property of *published* versions only.
