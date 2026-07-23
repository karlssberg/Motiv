import { test, expect, type APIRequestContext, type Page } from '@playwright/test';

const RULE_URL = '/api/rules/rules/can-checkout';

async function currentVersion(request: APIRequestContext): Promise<number> {
  const response = await request.get(RULE_URL);
  expect(response.ok()).toBe(true);
  return ((await response.json()) as { version: number }).version;
}

/**
 * Rules are per-process state on the running host, so normalize before AND after:
 * revert works from any state and always moves the version forward, giving the
 * test a known baseline (the compiled default) regardless of what earlier runs left behind.
 */
async function revertToDefault(request: APIRequestContext): Promise<number> {
  const base = await currentVersion(request);
  const response = await request.delete(`${RULE_URL}?baseVersion=${base}`);
  expect(response.ok()).toBe(true);
  return ((await response.json()) as { version: number }).version;
}

/** The rule-version badge in the header, e.g. "v3" or "v2 — code-defined default …". */
function versionBadge(page: Page, version: number) {
  return page.getByText(new RegExp(`^v${version}\\b`));
}

test.afterEach(async ({ request }) => {
  await revertToDefault(request);
});

test('editing and saving a rule changes the next checkout, and stale saves conflict', async ({ page, request }) => {
  // Known baseline: can-checkout on its compiled default (document is null server-side).
  const loadedVersion = await revertToDefault(request);

  await page.goto('/');

  // Load the live rule into the header: version + code-default note appear.
  await page.getByRole('combobox', { name: 'Rule', exact: true }).selectOption('can-checkout');
  await expect(versionBadge(page, loadedVersion)).toBeVisible();
  await expect(page.getByText(/code-defined default/)).toBeVisible();

  // Baseline: the compiled default (active AND adult) approves the sample customer.
  await page.getByRole('button', { name: 'Try checkout' }).click();
  await expect(page.getByText('Approved', { exact: true })).toBeVisible();

  // Make the rule impossible for the sample customer: NOT(is-active) via the node toolbar.
  await page.getByRole('button', { name: 'toggle NOT at $.rule' }).click();
  await expect(page.getByLabel('rule document')).toContainText('"not"');
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  const savedVersion = loadedVersion + 1;
  await expect(versionBadge(page, savedVersion)).toBeVisible();

  // The very next checkout reflects the swap — no restart happened.
  await page.getByRole('button', { name: 'Try checkout' }).click();
  await expect(page.getByText('Rejected', { exact: true })).toBeVisible();

  // A writer holding a stale version gets a 409 (simulated second tab via the API).
  const stale = await request.put(RULE_URL, {
    data: { document: { rule: { spec: 'is-active' } }, baseVersion: loadedVersion },
  });
  expect(stale.status()).toBe(409);
  expect(((await stale.json()) as { currentVersion: number }).currentVersion).toBe(savedVersion);

  // And the UI path shows the banner: another writer wins, then the UI saves a stale version.
  const winner = await request.put(RULE_URL, {
    data: { document: { rule: { spec: 'is-active' } }, baseVersion: savedVersion },
  });
  expect(winner.ok()).toBe(true);
  const winningVersion = savedVersion + 1;

  await page.getByRole('button', { name: 'Save', exact: true }).click();
  const banner = page.getByRole('alert');
  await expect(banner).toContainText(`Someone else saved version ${winningVersion}`);

  // Reload latest clears the conflict and adopts the winner's version.
  await page.getByRole('button', { name: 'Reload latest' }).click();
  await expect(banner).toBeHidden();
  await expect(versionBadge(page, winningVersion)).toBeVisible();
});
