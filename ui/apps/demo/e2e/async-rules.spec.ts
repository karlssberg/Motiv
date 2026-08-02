import { test, expect, type APIRequestContext, type Page } from '@playwright/test';
import { chooseFromPalette, closeDocument, openDocument } from './shell.js';

const RULE_URL = '/api/rules/rules/fraud-screening';

/** The registered async spec, under the namespaced name the sample's catalog serves it as. */
const ASYNC_SPEC = 'customer.passes-credit-check';

async function currentVersion(request: APIRequestContext): Promise<number> {
  const response = await request.get(RULE_URL);
  expect(response.ok()).toBe(true);
  return ((await response.json()) as { version: number }).version;
}

/** Rules are per-process state on the running host — normalize before AND after. */
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

test('an async rule validates and saves a document referencing an async spec', async ({ page, request }) => {
  // Known baseline: fraud-screening on its compiled default (document is null server-side).
  const loadedVersion = await revertToDefault(request);

  await page.goto('/');

  // Load the async live rule from the palette: version + code-default note appear. Loading an
  // async rule switches live validation to the async path (its default document is null, so
  // nothing is validated on the sync controller before the flag flips).
  await chooseFromPalette(page, 'Rules', 'fraud-screening');
  await expect(versionBadge(page, loadedVersion)).toBeVisible();
  await expect(page.getByText(/code-defined default/)).toBeVisible();

  // Point the draft at the registered async spec by typing it into the node's DSL row. The
  // catalog is namespaced, so the async spec answers to `customer.passes-credit-check` — the bare
  // leaf is not a registered name and would be rejected as UnknownSpec.
  await page.getByRole('button', { name: 'edit expression at $.rule' }).click();
  await page.keyboard.press('ControlOrMeta+a');
  await page.keyboard.type(ASYNC_SPEC);

  // Capture the validation the commit triggers, then commit.
  const validated = page.waitForResponse(
    (response) =>
      response.url().endsWith('/api/rules/validate') && response.request().method() === 'POST',
  );
  await page.keyboard.press('Enter');

  // The document and the error list share the one modal now, so a single open proves both that
  // the spec landed and that nothing was reported against it. Asserting the errors from outside
  // the modal would pass on the list simply not being mounted.
  const document = await openDocument(page);
  await expect(document).toContainText(ASYNC_SPEC);
  await expect(page.getByLabel('validation errors')).toBeHidden();
  await closeDocument(page);

  // The request carried the async flag, so the server bound via the async path and returned
  // no AsyncSpecInSyncLoad red herring — the exact seam this proves.
  const response = await validated;
  expect(response.request().postDataJSON().isAsync).toBe(true);
  expect(((await response.json()) as { errors: unknown[] }).errors).toEqual([]);

  // And the save binds the async spec as the rule's live implementation.
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(versionBadge(page, loadedVersion + 1)).toBeVisible();
});
