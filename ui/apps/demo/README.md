# Motiv Rules Demo

A three-pane demo of the Motiv rules-engine frontend: build a rule from the spec
catalog, watch it validate live, and evaluate it against a sample `Customer`.

## Run it

From the repo root, in two terminals:

```bash
# 1. The API + host (also serves the built SPA at http://localhost:5100)
dotnet run --project src/examples/Motiv.RulesEngine.Sample --urls http://localhost:5100

# 2. The demo with hot reload (proxies /api to the host)
pnpm -C ui/apps/demo dev
```

Open the Vite URL for development, or build the SPA into the host and use one origin:

```bash
pnpm -C ui/apps/demo build   # outputs into the host's wwwroot
# then browse http://localhost:5100
```

## Test

```bash
pnpm -C ui/apps/demo test    # component tests (jsdom)
pnpm -C ui/apps/demo e2e     # Playwright smoke (builds SPA, starts host)
```

The Playwright run downloads a browser on first use:
`pnpm -C ui/apps/demo exec playwright install chromium`.
