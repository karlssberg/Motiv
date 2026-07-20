import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  use: {
    baseURL: 'http://localhost:5100',
  },
  webServer: {
    command: 'dotnet run --project ../../../src/examples/Motiv.RulesEngine.Sample --urls http://localhost:5100',
    url: 'http://localhost:5100/api/rules/catalog',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
