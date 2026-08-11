# New app, or evolve the demo?

Type: grilling
Status: resolved
Blocked by: 07

## Question

Deferred from the charting session, because it is downstream of the boundary decision.

Does the flagship app grow out of `ui/apps/demo` + `src/examples/Motiv.RulesEngine.Sample`, or stand
beside them as a new host and SPA?

**The case for a new app.** A second consumer is the only thing that can *prove* a seam rather than
declare one — and the 218 : 6,819 ratio is the evidence that nothing has ever forced the packages to
carry product weight. The two artefacts' jobs also conflict directly: the demo's job is to be
understood in sixty seconds with zero configuration, and the first Trust & control ticket puts OIDC
in front of it. Eleven Playwright specs currently guard the demo; enterprise work will churn hard
over routing, nav, and permission-gating.

**The case against.** "New app" is not "write a new app" — it is "promote ~5,000 lines into packages
first, *then* write the app", and that migration blocks every enterprise capability behind it. Two
apps means two accessibility passes, two e2e suites, two dependency treadmills, permanently, on a
solo-maintained project. And the premise that the demo is minimal does not survive measurement: at
6,819 lines only its *shell* was made minimal.

**The resolution depends on 07.** If packages take the authoring experience, the migration is the
real work and a second app is cheap afterwards. If packages stay protocol-and-state, a second app
means rewriting the UI and evolving the demo is obviously right.

The session must also answer:

1. **What happens to the zero-config evaluation surface?** If the demo is consumed, does something
   replace it — a read-only public instance, a video, docs? Losing it silently is the failure mode.
2. **Does the backend split too?** `Motiv.RulesEngine.Sample` is 7 files. A new host is cheap even if
   a new SPA is not — the answer need not be symmetric across UI and backend.
3. **Naming and placement.** `src/examples/` is for examples. A flagship app is not an example.

Blocks: 18 (accessibility target).

## Inherited from ticket 07 — the calculus has changed

The case-against in this ticket assumed *"'new app' is not 'write a new app' — it is 'promote ~5,000
lines into packages first'"*, and that promotion meant moving UI. **It does not.** The boundary is
headless behaviour: components are not promoted, styling never becomes a package concern, and
CodeMirror stays app-side. Promotion is **behaviour extraction** — hooks and pure functions — not a
UI rewrite.

That cuts both ways and both should be weighed here:

- **For a new app:** the migration is smaller and less risky than charted, so the forcing-function
  argument gets cheaper. And whatever a second app renders, it renders itself — there is no shared
  component layer to fight over.
- **Against:** a second app must now build its *entire* UI from scratch, because the packages ship no
  components at all. The demo's 3,400 lines of markup are not reusable by a sibling app under this
  boundary — only the logic beneath them is.

Also relevant: the demo is not model-coupled (one constant, five files), so "the demo is welded to
its example" is not an argument available on either side.

## Answer

**Evolve the demo in place. It becomes `Motiv.Studio`, and a dev-mode identity keeps it
zero-config.**

### 1. Origin — evolve in place

Ticket 07 changed both sides of the argument recorded above.

*The case against a new app got weaker.* This ticket assumed promotion meant moving ~5,000 lines of
UI into packages first. It does not — under headless behaviour, promotion is **behaviour extraction**:
hooks and pure functions, no components, no styling story, no CodeMirror.

*And a different case against replaced it.* The packages ship **no components at all**, so a sibling
app must build its entire UI from scratch. The demo's ~3,400 lines of markup are not reusable by it;
only the logic beneath them is.

*The forcing-function argument does not survive the boundary change.* A second consumer buys
detection of one failure — the packages exporting something no real consumer can use, which is
exactly what happened to `RuleTree`. But a second full UI is an extremely expensive detector, and
that failure becomes far cheaper to catch once the boundary is *behaviour* rather than components:
if the app reimplements the accordion state machine instead of importing it, that is visible
duplication — greppable and testable. `RuleTree` went unnoticed precisely because a component either
gets imported or does not, leaving no trace when it does not. **Logic leaves a trace.**

Decisive practically: one UI to maintain, on a solo-maintained project. And the inventory found
`RuleHeader` + `PropositionsPage` (574 lines) are already *"precisely the governance app the
destination describes"* — the enterprise capabilities are additive to a UI that exists, not a
different UI.

Forking the demo as a seed was rejected for the worst-of-both property: two UIs that started
identical drift silently, and are the hardest kind to keep in sync.

### 2. The zero-config surface — a dev-mode identity, fail-closed

The app ships a development identity provider (a fixed signed-in admin, no IdP required) so
`docker compose up` still yields a working app in one command. **No separate demo is needed: the
product is the demo.** This is the option ticket 03 already anticipated.

**It must be fail-closed and loud** — refuse to start unless explicitly enabled, warn continuously
while active, and never be the default in a release-tagged image. Otherwise it is a default-credentials
vulnerability wearing a convenience label. → constrains ticket 03.

### 3. Naming and placement — `Motiv.Studio`

| from | to |
|---|---|
| `src/examples/Motiv.RulesEngine.Sample` | `src/Motiv.Studio` |
| `ui/apps/demo` | `ui/apps/studio` |
| `@motiv-rules/demo` | `@motiv-rules/studio` |

"Studio" is authoring-forward, which matches the product's centre of gravity — the builder and the
DSL surface, not operations dashboards. It leaves `src/examples/` holding genuine library examples
(`Motiv.Poker`, `Motiv.ECommerce`, `Motiv.SmartHome`), which is what that directory is for.

**The backend does not split.** The host is 285 lines of C# across three files; under evolve-in-place
there is one app and one host, so the question dissolves.

**Side effect to handle:** once it graduates, `src/examples/` has no hosted rules-engine example left,
while `docs/live-rules/AspNetCore.md` documents those endpoints. That likely wants a minimal snippet
in the docs rather than a whole project — but it is a real gap, not an oversight, and should be
closed deliberately.
