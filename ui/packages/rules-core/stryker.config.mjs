// StrykerJS mutation testing for @motiv-rules/core (#294). Run with `pnpm mutate` from this
// directory, or `pnpm -C ui/packages/rules-core mutate` from the repository root.
// See docs/contributing/mutation-testing.md.
export default {
  testRunner: 'vitest',
  // The default, '@stryker-mutator/*', globs the directory next to @stryker-mutator/core. pnpm's
  // isolated layout puts the runner somewhere else, so that glob finds nothing ("no TestRunner
  // plugins were loaded"). Resolve the runner from this package instead; a file URL is accepted.
  plugins: [import.meta.resolve('@stryker-mutator/vitest-runner')],
  // The vitest runner always analyses coverage per test; stated so the config says what happens.
  coverageAnalysis: 'perTest',
  mutate: ['src/**/*.ts'],
  // The sandbox is a copy of this package under .stryker-tmp/, test files included. By default a
  // failed run leaves it behind, and `vitest run` then collects the copied tests too. Always
  // remove it.
  cleanTempDir: 'always',
  reporters: ['html', 'json', 'progress', 'clear-text'],
  // Both are StrykerJS's defaults; pinned here because CI uploads these exact paths.
  htmlReporter: { fileName: 'reports/mutation/mutation.html' },
  jsonReporter: { fileName: 'reports/mutation/mutation.json' },
  // Report-only baseline: a low score never fails the run until the baseline has been triaged.
  thresholds: { high: 80, low: 60, break: null },
};
