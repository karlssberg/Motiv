# Motiv Studio

The flagship rules-governance app, and Motiv's own evaluation surface: build a
rule from the spec catalog, watch it validate live, evaluate it against a sample
`Customer`, and hot-swap the live rules the server executes — without a restart.

The host is `src/Motiv.Studio`; the authoring *logic* beneath this UI lives in
`@motiv-rules/core` (see **Extend it**).

## Layout

One surface, divided by hairlines: a top bar, the editor, and a rail beside it
holding the two panes that *run* the rule. The top bar carries the brand, the
page switch (Rules / Propositions, and Admin when the caller may administer
grants) and the page's actions — Open (`⌘K`), JSON and a primary Save. The
document being edited is named where it is edited: the editor pane's header
shows the rule or proposition, its model type and version. Below 900px the
rail stacks under the editor and the palette and dialogs fill the screen.

## Panes

- **Rules page** (`src/panes/RulesPage.tsx`) — owns the rule workflow: the
  rule in the route (`#/rules/<name>`) is loaded, document and version, into
  the editor, and saved back with a versioned `PUT`. With no rule in the route
  the page shows an empty state and a chooser — there is no local draft. A
  stale save surfaces as a conflict banner with a "Reload latest" escape hatch
  (open two tabs to watch the race protection work); a save the server rejects
  as invalid lists its errors in the JSON modal. Rules on a compiled default
  show a "code-defined default" note. Loading an async rule (e.g.
  `fraud-screening`) switches live validation to the async path, so documents
  may reference async specs without red herrings.
- **Document actions** (`src/shell/DocumentActions.tsx`) — the toolbar both
  document pages share: Open, JSON, Close, and Save as a split button whose
  menu offers *Save* and *Save & close*; the variant chosen becomes the
  button's default and is remembered per browser. Close on a document with
  unsaved changes asks first (`DiscardDialog`): Keep editing, Save & close, or
  Discard. "Unsaved" is the editor store's `dirty` flag, measured against the
  document last loaded or saved.
- **Editor** (`src/panes/EditorPane.tsx`) — the document under its title,
  edited either through the accordion builder (`BuilderPane.tsx`) or as DSL
  text, switched by the Builder / DSL tabs in its header.
- **JSON** (`src/panes/DocumentModal.tsx`) — the live document with validation
  errors, behind the toolbar's JSON action.
- **Evaluate** (`src/panes/EvaluatePane.tsx`) — evaluates the draft document
  against a sample model via `POST /api/rules/evaluate`; the outcome is a
  verdict chip beside the button, with the de-noised justification beneath.
- **Checkout** (`src/panes/CheckoutPane.tsx`) — the rule being *used*:
  `POST /api/checkout` executes the live `CanCheckoutRule` (sync) and
  `FraudScreeningRule` (async) on the server, and the decision reads the same
  way Evaluate's does. Save a rule change and the very next checkout reflects
  it.

Both JSON textareas (Evaluate's sample model and Checkout's customer) are
schema-enforced: the catalog's `modelTypes` map carries a JSON Schema for the
`customer` model, exported with the same serializer options the backend binds
with, and the panes run `validateAgainstSchema` from `@motiv-rules/core` before
posting. A mismatch (say `"age": "thirty"` where a number is expected — note
numeric *strings* like `"30"` are legal, matching the web binder) renders as
path-plus-message violations (`$.age: …`) and blocks the request. When the
catalog has no `modelTypes` (an older backend), enforcement simply doesn't run.

## Run it

From the repo root:

```bash
./run-studio.sh
# or
make studio
```

This builds the UI and serves it, together with the API, from a single host at
http://localhost:5100.

## With Docker

```bash
docker compose up
```

Then browse http://localhost:5100.

## Develop

For hot reload, run the host and the Vite dev server in two terminals:

```bash
# 1. The API + host (also serves the built SPA at http://localhost:5100)
dotnet run --project src/Motiv.Studio --urls http://localhost:5100

# 2. Studio with hot reload (proxies /api to the host)
pnpm -C ui/apps/studio dev
```

## Test

```bash
pnpm -C ui/apps/studio test    # component tests (jsdom)
pnpm -C ui/apps/studio e2e     # Playwright smoke (builds SPA, starts host)
pnpm -C ui/apps/studio a11y    # axe-core + keyboard suite (builds SPA, no host)
```

The Playwright run downloads a browser on first use:
`pnpm -C ui/apps/studio exec playwright install chromium`.

The e2e suite covers the smoke path (`e2e/smoke.spec.ts`), higher-order rules
(`e2e/higher-order.spec.ts`), and live rules (`e2e/live-rules.spec.ts`) — the
last proves the hot-swap story end to end: edit `can-checkout`, save, and the
next checkout flips without a restart; a stale save gets a `409` and the
conflict banner, and "Reload latest" adopts the winning version. Rules are
per-process state on the host, so the test reverts to the compiled default
before and after each run.

## Accessibility

`pnpm -C ui/apps/studio a11y` runs the accessibility gate (`e2e-a11y/`): `axe-core`
over every view and over each hard surface in the state it is hard in — the
palette open, the modal shown, the operator picker triggered — in both colour
schemes, plus a keyboard suite for what a scan cannot see, which is whether a
declared role behaves the way it promises. It needs no backend: it serves the
built bundle and answers the API from fixtures, so a finding is a fact about the
markup and its behaviour rather than about what a live store holds. The
`accessibility` job in `.github/workflows/ui.yml` runs it on every push.

That is about half of WCAG 2.1 AA. The other half — focus order, announcement
quality, whether a generated label means anything — is a scripted manual
screen-reader pass. Both, plus the conformance report and the two recorded gaps,
are in [`docs/accessibility`](../../../docs/accessibility/index.md).

## Extend it

The builder is the accordion under `ui/apps/studio/src/builder/`; load-bearing
seams are marked with code comments. Its *logic* — the accordion and highlight
state machines, node mutations and summaries, insertion planning, DSL sync,
completion and diagnostics — lives in `@motiv-rules/core`; what remains here is
rendering and the CodeMirror integration (`src/dsl/` maps the core's neutral
completion/diagnostic shapes onto CodeMirror's). `expression` and `parameters`
appear as disabled extension points in the UI, pending backend support.
