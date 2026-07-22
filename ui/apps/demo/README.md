# Motiv Rules Demo

A demo of the Motiv rules-engine frontend: build a rule from the spec catalog,
watch it validate live, evaluate it against a sample `Customer`, and hot-swap
the live rules the server executes — without a restart.

## Panes

- **Rule header** (`src/panes/RuleHeader.tsx`) — picks a live server rule,
  loads its document and version into the editor, and saves it back with a
  versioned `PUT`. A stale save surfaces as a conflict banner with a
  "Reload latest" escape hatch (open two tabs to watch the race protection
  work). Rules on a compiled default show a "code-defined default" note.
- **Builder** (`src/panes/BuilderPane.tsx`) — the accordion editor over the
  rule document.
- **JSON** (`src/panes/JsonPane.tsx`) — the live document with validation
  errors.
- **Evaluate** (`src/panes/EvaluatePane.tsx`) — evaluates the draft document
  against a sample model via `POST /api/rules/evaluate`.
- **Checkout** (`src/panes/CheckoutPane.tsx`) — the rule being *used*:
  `POST /api/checkout` executes the live `CanCheckoutRule` (sync) and
  `FraudScreeningRule` (async) on the server. Save a rule change and the very
  next checkout reflects it.

## Run it

From the repo root:

```bash
./run-demo.sh
# or
make demo
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
dotnet run --project src/examples/Motiv.RulesEngine.Sample --urls http://localhost:5100

# 2. The demo with hot reload (proxies /api to the host)
pnpm -C ui/apps/demo dev
```

## Test

```bash
pnpm -C ui/apps/demo test    # component tests (jsdom)
pnpm -C ui/apps/demo e2e     # Playwright smoke (builds SPA, starts host)
```

The Playwright run downloads a browser on first use:
`pnpm -C ui/apps/demo exec playwright install chromium`.

The e2e suite covers the smoke path (`e2e/smoke.spec.ts`), higher-order rules
(`e2e/higher-order.spec.ts`), and live rules (`e2e/live-rules.spec.ts`) — the
last proves the hot-swap story end to end: edit `can-checkout`, save, and the
next checkout flips without a restart; a stale save gets a `409` and the
conflict banner, and "Reload latest" adopts the winning version. Rules are
per-process state on the host, so the test reverts to the compiled default
before and after each run.

## Extend it

The builder is the accordion under `ui/apps/demo/src/builder/`; load-bearing
seams are marked with code comments. `expression` and `parameters` appear as
disabled extension points in the UI, pending backend support.
