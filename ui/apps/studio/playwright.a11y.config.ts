import { defineConfig } from '@playwright/test';

/**
 * The accessibility gate: `axe-core` over every Studio view and over each of the hard surfaces in
 * the state it is hard in, plus the keyboard suite that drives the patterns a scan cannot judge —
 * a role is a promise about behaviour, and a scan only ever sees the markup.
 *
 * A second config rather than a project inside `playwright.config.ts`, because the two suites need
 * different servers and Playwright's `webServer` is config-wide. The `e2e/` suite drives the real
 * .NET host, which is the only way to prove the endpoints; this one serves the built SPA and
 * answers the API from fixtures (`stubs.ts`), which is what makes it a *gate*: deterministic
 * findings, and no .NET SDK, so it runs on every pull request in the `ui` workflow rather than
 * waiting on a toolchain that workflow does not have.
 */

/**
 * The port the sweep's preview server listens on. Deliberately not 5100: that is the e2e suite's
 * and the dev harness's, and an audit that cannot be run while a server is already up is an audit
 * that gets skipped. Overridable for the same reason the e2e port is.
 */
const port = Number(process.env.MOTIV_A11Y_PORT ?? 4180);
const baseURL = `http://localhost:${port}`;

export default defineConfig({
  testDir: './e2e-a11y',
  timeout: 60_000,
  use: { baseURL },
  webServer: {
    // `vite preview` serves `build.outDir`, which is the host's `wwwroot` — the same bundle the
    // .NET host would serve, so the audit is of the shipped build and not of a dev-server variant
    // with its own error overlay and unminified stylesheet.
    command: `vite preview --port ${port} --strictPort`,
    url: baseURL,
    /**
     * Never adopt a server this suite did not start — the same rule, and the same reason, as the
     * e2e config: the preview server serves a build written *inside the checkout*, so one already
     * listening belongs to whichever checkout started it. An audit that passes against a bundle
     * that is not the one under test reports a green nobody would think to doubt.
     */
    reuseExistingServer: false,
    timeout: 60_000,
  },
});
