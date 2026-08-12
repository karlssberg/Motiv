import { defineConfig } from '@playwright/test';

/**
 * The port the suite's own host listens on. One constant rather than the same literal repeated in
 * three places, so the base URL, the launch command and the readiness probe cannot drift apart.
 *
 * Overridable because the default collides by design: 5100 is also the dev harness's preferred
 * port, and a second checkout of this repo has no other way to run the suite while the first has
 * a server up. Set `MOTIV_E2E_PORT` to any free port there.
 */
const port = Number(process.env.MOTIV_E2E_PORT ?? 5100);
const baseURL = `http://localhost:${port}`;

/**
 * The auth suite (e2e/auth.spec.ts) drives its own, separately-started stack
 * (`docker compose --profile auth up`, demo-auth on :5101 + Keycloak on :8081) via
 * MOTIV_E2E_AUTH_URL and needs no local dev-identity server at all. Starting this webServer
 * anyway would fight over host port 5100 with that stack's default `demo` service — the compose
 * file binds 5100:5100 unconditionally, profile or not — turning `pnpm e2e:auth` into a bind
 * error or a 120s webServer timeout before a single auth test runs. So: no MOTIV_E2E_AUTH_URL,
 * webServer as before; MOTIV_E2E_AUTH_URL set, omit it entirely. `--list` never starts a
 * webServer regardless, so listing auth.spec.ts alone was never affected by this.
 */
const webServer = process.env.MOTIV_E2E_AUTH_URL
  ? null
  : {
      command: `dotnet run --project ../../../src/examples/Motiv.RulesEngine.Sample --urls ${baseURL}`,
      env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development' },
      url: `${baseURL}/api/rules/catalog`,
      /**
       * Never adopt a server this suite did not start, even locally.
       *
       * This was `!process.env.CI`, which reads as a harmless local speed-up and is not one. The
       * host serves the UI from `wwwroot`, which `vite build` writes *inside the checkout* — so a
       * server already listening on this port belongs to whichever checkout started it and is
       * serving that checkout's bundle. Reusing it means the suite exercises a build that is not
       * the one under test, and reports a pass for it. Nothing about that failure announces
       * itself: the tests are green, against the wrong code.
       *
       * The cost is that a port already in use now fails the run instead of quietly borrowing it.
       * That is the point — a loud "port in use" is recoverable in seconds via MOTIV_E2E_PORT,
       * whereas a false green is not recoverable at all, because nobody goes looking.
       *
       * CI already behaved this way (`CI` is set there, so the flag was already false); this only
       * changes local runs, which is exactly where the hazard lived.
       */
      reuseExistingServer: false,
      timeout: 120_000,
    };

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  use: {
    baseURL,
  },
  ...(webServer ? { webServer } : {}),
});
