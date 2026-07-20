# Motiv Rules Demo

A three-pane demo of the Motiv rules-engine frontend: build a rule from the spec
catalog, watch it validate live, and evaluate it against a sample `Customer`.

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

## Extend it

The builder is the accordion under `ui/apps/demo/src/builder/`; load-bearing
seams are marked with code comments. `expression` and `parameters` appear as
disabled extension points in the UI, pending backend support.
