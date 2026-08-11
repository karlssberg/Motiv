# The namespace-prefix RBAC grant model

Type: grilling
Status: resolved
Blocked by: 03

## Question

Decided while charting: **permissions attach to dotted namespace prefixes** — `pricing.*`,
`fraud.*` — reusing the projection `namespaceTree.ts` already builds from proposition names. That
projection is deliberately stateless: *"a pure projection of the dotted names — there is no stored
hierarchy to keep in sync, so a rename is a move and nothing else has to know."*

**What is a grant, concretely?**

The session must resolve:

1. **The verbs.** Candidates: `read`, `evaluate`, `author` (save a draft), `approve`, `publish`,
   `delete`, `register-spec`, `administer`. Which are distinct, and which collapse? Note that
   `evaluate` on the execution surface may not be a namespace-scoped concept at all — see 03's
   machine-vs-human split.
2. **Do rules and propositions share a namespace?** Propositions are dotted
   (`customer.is-active`). Are rule names dotted too, and do they live in the same tree? If they
   do not, "namespace prefix" means two different things and the model fractures.
3. **Prefix semantics.** Does a grant on `pricing` cover `pricing.eu.vat`? (Almost certainly yes.)
   Are there deny rules, or is it grant-only? Deny rules make evaluation order significant and are a
   classic source of authorisation bugs — the burden of proof should be on including them.
4. **Specs are not namespaced by permission today.** `SpecRegistry.Register` is compile-time and
   anything registered is composable by anyone hitting `/catalog`. Does the catalogue become
   *filtered by grant*, and does an analyst composing a rule in `pricing.*` see specs from
   `fraud.*`? This is the "engineers author the guardrails" persona made real — and it is arguably
   the most product-defining question on this ticket.
5. **Where is the grant stored and who administers it?** In the app's store, or delegated to IdP
   claims/groups so the enterprise administers it where they already administer everything else?
   The latter is what self-hosted adopters usually want; it also means grants may not be queryable.
6. **What does the SDK expose versus the app implement?** Two-sidedness: an authorisation
   abstraction in the SDK, a reference grant store in the app.

Blocks: 13 (the `ChangeRequest` model).

## Inherited from ticket 03

- **Authorization lives in `Motiv.Serialization.AspNetCore`**, using ASP.NET policies and
  `ClaimsPrincipal`. The Motiv-specific piece this ticket designs is a **namespace-grant evaluator
  taking a principal** — not a new identity abstraction for adopters to implement.
- **Every endpoint is already authenticated**, secure-by-default with an explicit opt-out. So this
  ticket starts from "who may do what", not "is anyone authenticated".
- **The whole HTTP surface is authoring** — there is no machine execution path, since rules are
  executed in-process via DI. Sub-question 1's `evaluate` verb therefore covers *testing a draft*,
  not production evaluation, which changes what it should be scoped to.
- **Do not fold tenancy into the identity abstraction.** Tenancy selects which `BindingScope` you get;
  identity authorizes within it. Different axes, different layers.

## Answer

**A three-verb ladder — `read` → `author` → `publish` — over a dotted prefix shared by rules and
propositions. Grant-only, prefix-covering, no denies. Read is unfiltered; write is grant-gated. Grants
come from a swappable `IGrantSource`: an app-owned store by default, a shipped IdP-claims
implementation as an alternative, `administer` present only when the active source supports it.**

### Two facts from the code that reframed the ticket

- **The namespace tree is a *proposition* projection today.** `namespaceTree.ts` builds from
  `PropositionListEntry` only; rules are opaque store keys at `/rules/{name}` with no dot-schema and
  no path into that tree. "Namespace prefix covers rules" is a decision below, not existing reuse.
- **Nothing filters the catalogue.** `SpecRegistry` is a flat compile-time catalog; `/catalog`
  enumerates all of it. Any filtering is new machinery, not a flag.

### Sub-1 — the verbs: `read` → `author` → `publish`

Most candidates fold or turn out not to be namespace verbs:

| Candidate | Verdict |
|---|---|
| `read` | **Keep** — lowest tier; see rules/propositions/catalog under the prefix. |
| `evaluate` | **Fold.** It tests an *arbitrary draft document* (ticket 03), not a stored rule — not prefix-scoped. Gate on "authenticated + holds any `author` grant", not per-namespace. |
| `author` | **Keep** — create/edit/save drafts (non-live). |
| `approve` | **Fold into `publish` for v1.** Maker-checker (publisher ≠ author) is a *workflow* rule for ticket 13's `ChangeRequest`, not a separate grant. |
| `publish` | **Keep** — make drafts live; revert; delete live rules. The live-state tier. |
| `delete`/revert | **Fold into `publish`** — removing a *live* rule has publish's blast radius; deleting a *draft* is just `author`. |
| `register-spec` | **Not a runtime verb.** It is `SpecRegistry.Register` in C# at startup. The "engineers author the guardrails" persona is enforced at **compile/deploy time — who ships the host** — not by RBAC. |
| `administer` | **Conditional** — see sub-5; exists only when the active grant source is mutable. |

`read ⊂ author ⊂ publish` as a ladder: publish implies author implies read within a prefix.

### Sub-2 — rules and propositions share one namespace

**Unify.** Rule names are dotted and live in the *same* tree as propositions; a grant on `pricing.*`
authorizes writing both propositions and rules whose names fall under it. Two different prefix spaces
would fracture the model (the ticket's own warning), and ticket 02 already unifies rules and
propositions under one `BindingScope`, so one tree is consistent with the store shape.

The composition subtlety, resolved by sub-4: a rule `pricing.eu.vat` may *reference* `customer.is-active`
and `geo.in-eu`. Filtered-write means you need `author` on the rule's **own** namespace (`pricing.*`).
You need **no** grant on the referenced propositions' namespaces — reading and composing-from any
building block is unfiltered. **The write grant is a function of the artefact's own name, never of what
it references.** (Code change: feed rule names into the projection, which currently takes propositions
only.)

### Sub-3 — prefix semantics

**Grant-only, prefix-covering, no denies.** A grant on `pricing` covers `pricing.eu.vat`. No deny
rules — the ticket put the burden of proof on including them and nothing here discharges it; denies
make evaluation order significant and are a classic authorisation-bug source.

### Sub-4 — unfiltered read, filtered write *(user decision)*

Every authenticated user sees the whole spec catalogue and composes from any building block; grants
gate only which prefixes they may **save/publish** rules into. Specs are shared vocabulary
(`customer.is-active` is useful everywhere); hiding them fractures composition, and it would bolt
namespace-grant logic onto the flat compile-time `SpecRegistry`. **The guardrail is where your rule
lands, not what you can see.**

Consequence for the hot path: the grant evaluator is consulted only on **write** paths and to compute
"which prefixes may I author into" for the authoring UI. Read/`/catalog` never touch it; the sole read
use is the coarse "can this user author *anywhere*", which gates the evaluate sandbox and whether
authoring UI renders at all.

### Sub-5 / Sub-6 — `IGrantSource`, app-owned default, IdP reference impl *(user decision)*

The source of truth is **swappable behind an SDK abstraction**, not a fixed choice:

- **`IGrantSource`** (SDK) yields the `(subject-or-group → prefix → verb)` grants for a principal. The
  **grant evaluator** — `(ClaimsPrincipal, verb, prefix) → allow/deny`, prefix-covering — lives in
  `Motiv.Serialization.AspNetCore` using ASP.NET policies (ticket 03), consuming the active source.
- **App-owned store is the default** implementation: mutable, enumerable, queryable ("who can edit
  `pricing.*`?"), with an admin surface.
- **A shipped IdP-claims implementation**: reads group/role claims off the principal and runs them
  through a **configured claims→prefix mapping** (the mapping lives in app config, since the IdP does
  not know Motiv's namespaces). Desirable because self-hosted enterprises usually want their IdP as
  the source, and a claims-mapper is reusable — worth shipping rather than leaving each adopter to
  build.
- **A dev-only single-user source**: the authorization-side twin of ticket 08's dev identity. It
  closes a real gap — the app-owned store boots **empty**, so `docker compose up` on the dev identity
  alone yields an authenticated-but-powerless user who can author nothing. The dev source grants the
  single known dev principal everything, zero-config. **A dedicated source, not a seed of the
  app-owned store**: seeding writes a *persisted* superuser row that survives into any store carried to
  production; a source-backed grant exists only while the dev source is *active* and evaporates the
  moment the switch is off — never persisted. This mirrors why ticket 08 chose a dev identity
  *provider* over seeding a dev user into the real user store. It is **single-superuser on purpose** —
  RBAC-ladder correctness (author≠publish, prefix isolation) is tested against ticket 03's Keycloak
  e2e, not simulated here; a config-backed multi-persona dev source is YAGNI until local iteration
  proves painful. Being immutable, it has no `administer` surface (per the rule below), so a leaked dev
  superuser can read/author/publish but cannot persist new grants — blast radius capped at the switch.
  **Inherits ticket 08's enforcement verbatim, and the bar is higher**: refuse to start unless
  explicitly enabled, warn continuously, never the default in a release-tagged image — a leaked dev
  identity is *a* user, a leaked dev grant is an *admin*.
- **`administer` is a capability of the active source, not a universal verb.** Mutable/enumerable
  source → `administer` and the "who can do what" query exist. External (IdP) source → no in-app
  `administer`; that query degrades to "administer it in your IdP". The abstraction carries a
  capability flag so the verb and admin UI light up or don't accordingly.

Future-permitted, not v1: a **composite `IGrantSource`** (coarse IdP roles + fine app-store overrides,
a real enterprise pattern) is expressible by writing one implementation over two, without v1 shipping
it.

### Two-sidedness (sub-6), concretely

- **SDK** (`Motiv.Serialization` / `.AspNetCore`): `IGrantSource`, the prefix-covering grant evaluator,
  the ASP.NET policy wiring, the source-capability flag.
- **App**: the app-owned grant store (default), the IdP-claims reference source, the dev-only
  single-user source (twin of ticket 08's dev identity, same fail-closed switch), and the admin surface
  that appears only for a mutable source.

## Downstream

- **Ticket 13 (`ChangeRequest`)** inherits: `approve` is *not* a grant — maker-checker (publisher ≠
  author) is enforced by 13's workflow over the `author`/`publish` split. Segregation-of-duties is a
  workflow property here, not an RBAC verb.
- **Ticket 03**: confirms the grant evaluator's placement and that no new identity abstraction ships;
  `IGrantSource` is authorization-data, distinct from the tenancy seam that selects `BindingScope`.
