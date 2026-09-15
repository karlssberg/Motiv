import { test, expect, type APIRequestContext, type Page } from '@playwright/test';
import { chooseFromPalette, expectDocument } from './shell.js';

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

/** The scenario table's Live cell for one named scenario: the running rule's verdict for it. */
function liveVerdict(page: Page, scenario: string) {
  return page.getByRole('row', { name: scenario, exact: true }).getByRole('cell', { name: 'live' });
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

  // Load the live rule from the toolbar's palette: version + code-default note appear.
  await chooseFromPalette(page, 'Rules', 'can-checkout');
  await expect(versionBadge(page, loadedVersion)).toBeVisible();
  await expect(page.getByText(/code-defined default/)).toBeVisible();

  // Baseline: the compiled default (active AND adult) approves the seeded active adult — read off
  // the scenario table's Live column, which is what the server decides for this rule right now.
  await page.getByRole('button', { name: 'Run all' }).click();
  await expect(liveVerdict(page, 'Active adult, 3 orders')).toHaveText(/yes/);

  // Make the rule impossible for the sample customer: negate it by typing into its row.
  await page.getByRole('button', { name: 'edit expression at $.rule' }).click();
  await page.keyboard.press('ControlOrMeta+a');
  await page.keyboard.type('!customer.is-active');
  await page.keyboard.press('Enter');
  await expectDocument(page, '"not"');
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  const savedVersion = loadedVersion + 1;
  await expect(versionBadge(page, savedVersion)).toBeVisible();

  // The very next live evaluation reflects the swap — no restart happened.
  await page.getByRole('button', { name: 'Run all' }).click();
  await expect(liveVerdict(page, 'Active adult, 3 orders')).toHaveText(/no/);

  // A writer holding a stale version gets a 409 (simulated second tab via the API).
  const stale = await request.put(RULE_URL, {
    data: { document: { rule: { spec: 'customer.is-active' } }, baseVersion: loadedVersion },
  });
  expect(stale.status()).toBe(409);
  expect(((await stale.json()) as { currentVersion: number }).currentVersion).toBe(savedVersion);

  // And the UI path shows the banner: another writer wins, then the UI saves a stale version.
  const winner = await request.put(RULE_URL, {
    data: { document: { rule: { spec: 'customer.is-active' } }, baseVersion: savedVersion },
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

test('every rules response carries the generation it was served from', async ({ request }) => {
  const response = await request.get('/api/rules/rules');
  expect(response.headers()['motiv-generation']).toMatch(/^r\d+\.p\d+$/);
});
