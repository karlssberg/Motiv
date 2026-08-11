# SDK public-API stability and semver policy

Type: grilling
Status: resolved
Blocked by: —

## Question

An enterprise adopts a library on the strength of its upgrade story. Motiv versions from git tags via
MinVer and treats warnings as errors, but there is no stated compatibility policy and nothing
mechanically prevents an accidental breaking change.

**What does Motiv promise about its public surface, and what enforces the promise?**

Sub-questions:

1. **A public-API surface test.** Approved-API snapshot (`PublicApiGenerator` / `Verify`) so a
   breaking change fails CI rather than shipping. Which assemblies — `Motiv`,
   `Motiv.Serialization`, `Motiv.Serialization.AspNetCore` — and does the same discipline apply to
   the npm packages' `.d.ts`?
2. **What counts as public?** `[InternalsVisibleTo]` is used liberally for tests. Nothing else is
   ambiguous in .NET — but in TypeScript, `exports` in `package.json` is the only gate and
   `rules-core`'s `index.ts` re-exports 12 modules. Is everything exported public, or is there a
   `/internal` subpath?
3. **The HTTP surface is an API too.** `/catalog`, `/validate`, `/evaluate`, the rules and
   propositions endpoints, and the `RuleDocument` JSON schema are consumed by clients the repo does
   not own. Do they get versioning (`/v1`), and is the rule-document schema versioned independently
   of the package?
4. **Pre-1.0 or 1.0?** MIT libraries below 1.0 are conventionally allowed to break. "Enterprise-grade"
   arguably *requires* 1.0. Does this effort commit to 1.0, and what is the deprecation window —
   `[Obsolete]` for one minor, one major, or forever? Note the precedent already set: `IsSatisfiedBy`
   is retained as an `[Obsolete]` shim rather than removed.
5. **TFM and runtime matrix.** `Motiv.Serialization.AspNetCore` targets `net10.0` only. Enterprises
   run LTS. Is a `net8.0` target in scope, and what is the support window?

Blocks nothing directly, but the fog patch "docs, adoption, and upgrade path" waits on it.

## HTTP surface warts, found while working ticket 03

Sub-question 3 treats the HTTP surface as public API. Two things in it are worth deciding on
deliberately before a compatibility policy freezes them.

### `DELETE /rules/{name}` does not delete

It calls `RuleSet.Revert`. The rule still exists, back on its compiled default — and per ticket 02
its **version moves forward, never back**. A `DELETE` that increments a version and leaves the
resource present will surprise every client author who has not read the source.

It already costs something concrete: the ticket 01 inventory found `PropositionsPage` needs
*"revert-vs-delete disambiguation read off the entry, because the `DELETE` response cannot tell them
apart."* The client has to reconstruct which of two different operations occurred.

Options: rename to something honest (`POST /rules/{name}/revert`), keep `DELETE` and document it
loudly, or make the response distinguish the outcomes so the client stops inferring.

### `POST /propositions` carries the name in the body

Unusual — `POST` to a collection normally implies server-assigned identity, and here the client
supplies the name. **Do not "fix" this without noticing what it buys:** separating create from update
makes accidental overwrite impossible. A `PUT`-upsert would let a mistyped name that happens to exist
silently replace another author's proposition; the current split returns 409 instead. That protection
becomes more valuable, not less, once ticket 12 makes propositions a governed, permissioned resource.

### The verb asymmetry itself is correct and should be preserved

Propositions have `POST` (create); rules do not. This is ticket 02's distinction expressed in HTTP —
a rule is **code with a mutable document slot**, its set fixed at startup by `AddRule<T>()`, so there
is nothing for `POST` to create; a proposition is **data all the way down** and does not exist until
authored. Any future symmetry-tidying that adds `POST /rules` would be wrong.

Note also that idempotency does *not* distinguish these: `PUT` and `DELETE` both carry a
`baseVersion` precondition (the analogue of `If-Match`), and `POST` 409s on a duplicate name, so all
three leave server state unchanged on replay.

### `/api/rules/rules/{name}` — the base path stutters

`Program.cs` calls `app.MapMotivRules("/api/rules")`, and the SDK mounts `catalog`, `validate`,
`evaluate`, `rules` and `propositions` as siblings beneath the base. So the base is named after *one
of its own five children*, and rule URLs read `/api/rules/rules/{name}`.

The SDK is not at fault — `MapMotivRules` lays the children out correctly under whatever base it is
given. The **sample chose the base badly**. A base should name the product area so its children can
name resources; the same mistake also produces `/api/rules/propositions/{name}`, which is as odd as
the stutter and only less visible because the words differ. `/api/motiv` would fix both.

Already load-bearing in `README.md:238`, `ui/apps/demo/e2e/live-rules.spec.ts`,
`e2e/async-rules.spec.ts`, four assertions in `ui/packages/rules-core/test/client.test.ts`, and
`AppRules.cs`'s own doc comment (*"hot-swappable via PUT /api/rules/rules/can-checkout"*).

**Urgency:** ticket 08 makes the sample the flagship app, so this stops being a demo quirk and becomes
the product's public URL — and this ticket's compatibility policy then freezes it. Changing a base
path is a one-line edit plus a find-and-replace today, and a breaking change for every client the
moment it ships. Decide it before, not after.

## Answer

### 0. The publication facts this ticket turned on

| package | NuGet |
|---|---|
| `Motiv` | **published — v8.0.0, 22 versions** |
| `Motiv.Serialization` · `.AspNetCore` · `Analyzer` · `CodeFix` | **never published** |
| `@motiv-rules/core` · `/react` | never published (ticket 22) |

**Everything the enterprise work touches has never shipped.** This corrects two earlier records:
ticket 09 justified seven breaking signatures on `RuleSet`/`PropositionSet` as "cheap because
pre-1.0" — they are in an unpublished package and cost **nothing**; ticket 03 called
secure-by-default "breaking for every existing adopter" — there are **no adopters**. Right
conclusions, wrong reasons.

### 1. Public surface — curate, then snapshot

`rules-core/index.ts` is `export * from` over 12 modules, publishing **100 symbols** nobody chose
(`document.ts` alone contributes 35). `rules-react` is already explicit, at 7.

**Replace the barrel with explicit named exports, and add approved-API snapshot tests** —
`PublicApiGenerator`/`Verify` for the three packable assemblies, a `.d.ts` snapshot for the npm
packages — so any surface change fails CI.

**Sequencing is the decision.** Ticket 07 promotes ~3,400 lines *into* `rules-core`; under `export *`
every symbol it exports becomes public API on arrival, chosen by nobody. Curate **before** the
promotion and each promoted symbol is deliberate. Curate after, and it is a removal from a published
surface.

`RuleTree` is removed here rather than deprecated: ticket 07 made it inconsistent with the boundary
and its only consumer never imported it, so nothing depends on it.

### 2. Versioning — two trains

`Motiv` keeps its `v` tag prefix and v8 line. The rules stack (`Motiv.Serialization`, `.AspNetCore`,
`Analyzer`, `CodeFix`) moves to its own `MinVerTagPrefix` and starts at **0.x**, reaching 1.0 when the
enterprise work settles.

Why not lockstep: MinVer derives every version from one tag, so publishing `Motiv.Serialization`
today would ship a *first release numbered 8.0.0*, and — worse — every deliberate break in the rules
stack would drag **`Motiv` to 9.0.0, 10.0.0, 11.0.0**, signalling migration to 22 releases' worth of
adopters whose package did not change.

Deprecation window: the existing `[Obsolete("Use Evaluate instead.")]` sites (3, `IsSatisfiedBy`)
stay. Policy for `Motiv` is one major's notice before removal; the 0.x rules stack may break in
minors, which is what 0.x means.

### 3. Target frameworks — LTS only, stated as a promise

| | |
|---|---|
| drop | `net9.0` everywhere — **already EOL** (STS, ended May 2026), pure build-and-test cost |
| keep | `net8.0` (LTS to Nov 2026) and `net10.0` (LTS to Nov 2028) |
| keep | `netstandard2.0` — the `net472` test target shows the .NET Framework audience is real |
| add | `net8.0` to `Motiv.Serialization.AspNetCore` |

The AspNetCore package is net10.0-only because of **one call**:
`MotivRulesEndpoints.cs:265` uses `GetJsonSchemaAsNode` (`System.Text.Json.Schema`, .NET 9+). Guard it
with `#if NET9_0_OR_GREATER` and a net8.0 build simply omits `modelTypes` from the catalog — **the
client already handles precisely that**: *"When the catalog has no `modelTypes` (an older backend),
enforcement simply doesn't run."*

State it as *"we support the LTS releases"*. The current matrix expresses no policy, and an auditor
comparing it to Microsoft's lifecycle page finds a dead target in it.

### 4. HTTP surface — fix now, version at 1.0

**Immediately, while there are zero adopters:**

- **Pin the schema `$id`.** It points at `raw.githubusercontent.com/.../main/schemas/rule.v1.json` —
  a *mutable ref*. A file named `v1` served from `main` means any schema change retroactively alters
  the meaning of every document citing it. Point it at a tag or an immutable `/v1/` path. Cheapest
  item here and the only irreversible one.
- **Fix the base-path stutter.** `/api/rules/rules/{name}` — the sample names its base after one of
  the SDK's own five children. `/api/motiv` fixes it and also fixes `/api/rules/propositions`.
- **Rename `DELETE /rules/{name}`.** It calls `Revert`: the rule survives and its version moves
  *forward*. Something honest — `POST /rules/{name}/revert` — or a response that distinguishes the
  outcomes, so clients stop inferring (the ticket 01 inventory found `PropositionsPage` doing exactly
  that inference).

**Preserved deliberately:** `POST /propositions` carrying the name in the body, and the absence of
`POST /rules`. The first makes accidental overwrite impossible; the second is ticket 02's code/data
distinction in HTTP form. Neither is a wart. Do not tidy them.

**Deferred to the rules stack's 1.0:** path versioning (`/v1`), and making `$schema` actually
validated rather than merely checked to be a string. Adding `/v1` now would freeze paths while
tickets 12, 13 and 15 are still likely to add endpoints.
