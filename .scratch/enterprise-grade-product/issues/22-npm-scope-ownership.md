# npm scope ownership — is `@motiv` ours?

Type: task
Status: resolved
Blocked by: —

## Question

Gates first publish of `@motiv/rules-core` and `@motiv/rules-react`. Nothing downstream can ship
until it is settled, and after first publish a rename costs every adopter a migration.

### Established (checked 2026-08-07, unauthenticated)

| | |
|---|---|
| unscoped `motiv` | **v1.0.1 exists** — *"A simple cli application to make your day happier and better"*, maintainer `feliborgez`. **Not a blocker**: unscoped names and scopes are separate namespaces |
| `@motiv` scope | **claimed** — `registry.npmjs.org/-/org/motiv/user` returns `{}`, matching `babel`; a free scope returns `{"error":"Scope not found"}` |
| packages under `@motiv` | **zero** (`/-/v1/search?text=scope:motiv` → total 0). Reserved, dormant |
| `@motiv/rules-core`, `@motiv/rules-react` | unpublished (404) |
| NuGet `Motiv` | **ours** — v8.0.0 shipped 2026-06-28. The .NET side is unaffected |

### The manual step

No npm auth on the dev machine, so ownership could not be determined. Run:

```bash
npm login && npm org ls motiv
```

**If the scope is ours — close this ticket.** There is nothing to decide.

## If it is not ours

npm has a dispute process for abandoned *package names*; it has no comparable process for **orgs**.
A dormant scope held by a third party is effectively unreclaimable, so the realistic options are all
renames, and all of them must happen **before first publish**:

1. **A different scope** — `@motiv-rules/*`, or a personal scope `@karlssberg/motiv-*`. Keeps the
   brand, costs a hyphen. Check availability the same way before committing.
2. **Unscoped names** — `motiv-rules-core`, `motiv-rules-react`. No scope dependency at all, and
   unscoped names are individually claimable. Weaker grouping, and each must be checked separately.
3. **A distinct name for the JS side.** The .NET packages stay `Motiv.*` on NuGet; the npm packages
   take their own identity. Honest, and costs brand coherence across the two ecosystems.
4. **Contact the scope owner.** Zero published packages suggests a reservation rather than a product;
   a transfer may simply be a matter of asking. Low probability, near-zero cost to try.

Whatever is chosen, **reserve it immediately** by publishing a placeholder — the failure mode here is
deciding a name and losing it in the interval before first release.

### Feeds

Ticket 06 (API stability and semver policy): package names are the most public part of a public API,
and the compatibility policy cannot be stated until the names are settled. Also the fog patch
"docs, adoption, and the upgrade path" — every install line in every document depends on this.

## Answer

**The `@motiv` scope belongs to a third party** — confirmed by the maintainer, 2026-08-07. It holds
zero published packages, so it is a dormant reservation, and npm has no dispute process for orgs
comparable to the one for abandoned package names. Treat it as unreclaimable.

### Decision: `@motiv-rules/core` and `@motiv-rules/react`

Availability verified the same day — scope free, and so were `@motivjs`, `@motiv-engine`,
`@motiv-io`, `@motivlang`, `@karlssberg`, plus unscoped `motiv-rules-core`, `motiv-rules-react`,
`motiv-rules`, `motiv-spec`, `motiv-engine`. The choice was made from a full field, not forced.

A **scope** was preferred over unscoped names because ticket 07 makes companion packages likely
rather than hypothetical: choosing neutral shapes over a CodeMirror dependency means
`@motiv-rules/codemirror` is a natural future addition, and ticket 17 may add `@motiv-rules/vue`.
A scope accommodates those at no further cost; unscoped names would each be a separate claim in a
namespace where `motiv` is already gone.

`motiv-rules` also describes what the packages *are*, which the bare `@motiv` never did.

### Rename map

| from | to |
|---|---|
| `@motiv/rules-core` | `@motiv-rules/core` |
| `@motiv/rules-react` | `@motiv-rules/react` |
| — | `@motiv-rules/react/workflow` (subpath, per ticket 07) |

Neither package has ever been published, so this costs nothing beyond the edit: two
`package.json` names, the `workspace:*` dependency in `rules-react`, the demo's dependencies, and
every import across `ui/apps/demo/src` (46 files import from one or both).

**The .NET side is unaffected.** `Motiv` on NuGet is ours and shipped v8.0.0.

### Immediate action, ahead of any other work

**Register the scope and reserve it with a placeholder publish.** The failure mode this ticket exists
to prevent is deciding a name and losing it in the interval before first release — and that interval
is now open. Nothing else on this map is time-sensitive in the same way.

### Feeds

Ticket 06 — package names are the most public part of a public API, so the compatibility policy
cannot be stated until this lands. Also the fog patch "docs, adoption, and the upgrade path": every
install line in every document depends on it.

### Correction — how to actually create the scope

`npm org create` **does not exist**. The npm CLI's `org` command supports only `set`, `rm` and `ls`,
all of which manage members of an *already existing* org.

**Org scopes are created through the website only:** <https://www.npmjs.com/org/create> — name
`motiv-rules`, free plan (unlimited *public* packages; the paid tier is for private ones).

Then, because scoped packages default to `restricted` and restricted requires a paid plan, the
reserving publish must be explicit:

```bash
npm publish --access public
```

**User scopes behave differently and this is the practical distinction between the two options
weighed above.** A user scope exists automatically with the account — `@karlssberg` already reports
`{"karlssberg":"owner"}` — so `@karlssberg/*` is publishable immediately with no browser step, while
an org scope needs the web UI first. That is the whole cost difference between the chosen name and
the personal-scope fallback, and it keeps the fallback genuinely available.

Status as of 2026-08-07: logged in as `karlssberg`; `@motiv-rules` re-checked and **still free**;
org not yet created.

### Done — and a second correction: no reserving publish is needed

**`motiv-rules` org created 2026-08-08**; `npm org ls motiv-rules` returns `karlssberg - owner`. The
scope is held.

The advice above to "reserve it immediately by publishing a placeholder" was **wrong for a scoped
package**, and the counter-evidence was in hand the whole time: **`@motiv` holds zero published
packages and is still completely unusable by us.** Org ownership alone reserves the scope — only org
members may publish under it.

The distinction, recorded so it is not re-derived:

- **Unscoped names** are claimed per package, so reserving one *does* require a publish.
- **Scopes** are claimed by owning the org. Existing is sufficient.

**Do not publish yet.** Beyond being unnecessary, a first publish now would be premature:

- Ticket 06 (API stability and semver policy) is unresolved, so a published `0.1.0` would make a
  compatibility promise that has not been decided.
- Ticket 07 restructures these packages substantially — roughly 3,400 lines promoted from the demo,
  plus a `/workflow` subpath — so anything published now is superseded by design.
- npm `unpublish` is heavily restricted and broadly impossible after 72 hours, making a premature
  version effectively permanent and installable by anyone who finds it.

**First publish should follow ticket 06 and the ticket 07 promotion, not precede them.**

Nothing on this ticket remains open. PR #94 carries the rename and is ready to merge.
