# Bundle Spec — Surface Quality

Status: draft — synthesis of resolved decisions; no new architecture.
Source tickets: [01](../issues/01-demo-promotion-inventory.md) · [07](../issues/07-sdk-app-boundary.md) · [08](../issues/08-new-app-or-evolve-demo.md) · [17](../issues/17-non-react-story.md) · [18](../issues/18-accessibility-target.md) · [22](../issues/22-npm-scope-ownership.md)

## 1. Capability

A flagship self-hosted app (`Motiv.Studio`) that *is* the evaluation surface, built on **headless
packages that own the authoring logic and render nothing** — so the logic is reusable across runtimes
and the app is the reference UI. Accessibility is a first-class, enforced target because it is a
procurement gate.

## 2. Packaging & boundary

### The boundary (07)
- **Headless behaviour: the packages own the logic of authoring and render nothing.** Scope is
  **domain *and* workflow** (path arithmetic, insertion rules, the accordion state machine, DSL sync,
  completion, lint, token runs, vocabulary — *and* optimistic save, 409 recovery, blast-radius
  reporting), split across entry points so an adopter can take document logic without session opinions.
- **CodeMirror via neutral shapes**: packages declare their own completion/diagnostic/token-run types
  and take no CM dependency, even at the type level (`dslTokens.ts` proves it).
- Layout: framework-free logic → `rules-core`; React bindings → `rules-react`; workflow → a subpath.
- `RuleTree` is **removed** (its only consumer rejected it); `JustificationTree`'s survival is ticket
  06's call.

### The app (08) & inventory (01)
- **Evolve in place**: the demo becomes the flagship, renamed **`Motiv.Studio`** (`src/Motiv.Studio`,
  `ui/apps/studio`), out of `src/examples/`. No second app (logic leaves a trace; an unused component
  does not). The backend does not split (one host). Gap to close: `src/examples/` loses its only hosted
  rules-engine example.
- Inventory basis: **65% product-generic**; excluding stylesheets only 4.6% of TS is demo-specific;
  model coupling is a single `MODEL_TYPE = 'customer'` constant. The real promotion cost is unscoped
  class names against a global stylesheet and the CodeMirror packages — both handled by the boundary.

### npm scope (22)
- Publish as **`@motiv-rules/core`** and **`@motiv-rules/react`** (the `@motiv` scope is a third
  party's, unreclaimable). Org created; scope held. First publish follows ticket 06's curation and the
  ticket 07 promotion. NuGet `Motiv` is unaffected.

## 3. The non-React story (17) — a two-runtime story; both cores already exist

The DSL/schema exists in **both** TypeScript (`rules-core`) and C# (`Motiv.Serialization`), kept in sync
by ticket 06's pinned JSON schema `$id`. So support is answered per *runtime*:

- **React — supported** (the one adapter Motiv maintains and tests).
- **Vue / Svelte / vanilla — enabled, not supported.** The store is *verified framework-free*
  (`editor.ts`: `subscribe`/`getState`, no `useSyncExternalStore` caching baked in, no `react` import),
  so an adapter is ~200 bindings-only lines. Ship *one* second adapter (Vue) as a credibility signal if
  resourced; the neutral core is the deliverable, not the adapter.
- **Blazor / .NET — the better fit for the buyer, nearly free**: a Blazor WASM consumer uses
  `Motiv.Serialization` (C#) directly and needs `rules-core` at all.
- **Web components rejected** (packages are headless; would tax the React consumer).
- **Deliverable**: the honest support-tier table — the illegitimate thing is leaving it undocumented.

## 4. Accessibility (18) — WCAG 2.1 AA, enforced

- **Target**: WCAG 2.1 AA floor; a **VPAT / Accessibility Conformance Report is an explicit output**
  (procurement asks for the document).
- **The accordion builder is *not* `role=tree`** (wrong model for an editing surface with interactive
  nodes) — nested labeled `group`s + `disclosure` for navigation, and **Motiv's own generated
  `Reason`/`Justification` text is the authoritative accessible description of the composition** (the
  explainability output doubles as the a11y affordance).
- **CodeMirror a11y inherited, not invented**; command palette via `listbox`/`aria-activedescendant`
  + live-region announcements; modal via standard dialog focus-trap.
- **Enforcement = mechanical + manual, both required**: wire **`axe-core` into the existing Playwright
  suite** (not present today) for the ~50% it catches, **plus a required manual screen-reader pass**
  (NVDA/VoiceOver) on the accordion, CodeMirror strip, command palette, and modal for focus order,
  announcement quality, and labels on generated content. The VPAT is produced from both.
- **SDK carries none** (packages headless — a real cost, documented honestly): an adopter building their
  own UI gets no a11y help. The one exception is `JustificationTree` (read-only → the tractable case;
  the lone package-inherited a11y *if* it survives ticket 06).

## 5. Invariants (must hold)

- Packages render nothing; all authoring *logic* lives in them, all *rendering* in consumers.
- The TS and C# cores agree on one JSON schema (`$id`) — load-bearing for the Blazor story.
- `Motiv.Studio` is the product *and* the zero-config evaluation surface (fail-closed dev identity, from
  the Trust bundle).
- AA is enforced in CI (axe) and by manual audit; the VPAT reflects both.

## 6. New machinery / work to build

- Promote domain+workflow logic into `rules-core` (+ workflow subpath) per the boundary; keep
  `rules-react` bindings-only; remove `RuleTree`.
- Rename/publish `@motiv-rules/core` + `@motiv-rules/react` (curate the barrel first — ticket 06).
- Rehome the app as `Motiv.Studio`; add a replacement hosted example to `src/examples/`.
- Wire `axe-core` into Playwright; run the manual screen-reader audit; author the VPAT.
- (Optional) a `rules-vue` adapter as a credibility signal.

## 7. Verification obligations

- `rules-core` builds and its tests pass with **no** React present (framework-freeness is enforced, not
  asserted).
- `axe-core` passes on every `Motiv.Studio` view in CI; the manual audit signs off the four hard
  surfaces.
- A screen-reader user can read a rule's composition via its generated `Reason`/`Justification`.
- The Blazor sample authors a valid rule document through `Motiv.Serialization` alone (no `rules-core`).
