/// <reference types="vitest/config" />
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const packageSource = (name: string): string =>
  fileURLToPath(new URL(`../../packages/${name}/src/index.ts`, import.meta.url));

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://localhost:5100',
    },
  },
  build: {
    outDir: '../../../src/Motiv.Studio/wwwroot',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./test/setup.ts'],
    include: ['test/**/*.test.{ts,tsx}'],
    // Resolve the workspace packages to their TypeScript sources. Their `main`/`module`
    // fields point at `dist/`, which is gitignored and built by a separate `pnpm build`,
    // so tests would otherwise run against a stale artefact — or fail outright on a fresh
    // clone. Only tests are aliased; `vite build` still consumes the published entry points.
    // The subpath entries are listed before the roots: a plain-string alias also matches the
    // ids that extend it, appending the remainder to the replacement — so the root alias alone
    // would resolve `@motiv-rules/core/workflow` to `src/index.ts/workflow`.
    alias: {
      '@motiv-rules/core/workflow': fileURLToPath(
        new URL('../../packages/rules-core/src/workflow/index.ts', import.meta.url),
      ),
      '@motiv-rules/react/workflow': fileURLToPath(
        new URL('../../packages/rules-react/src/workflow/index.ts', import.meta.url),
      ),
      '@motiv-rules/core': packageSource('rules-core'),
      '@motiv-rules/react': packageSource('rules-react'),
    },
  },
});
