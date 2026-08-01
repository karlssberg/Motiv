import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import { openDsl, replaceBuffer } from './dsl-surface.js';

const API = '/api/rules';

/**
 * The live rule this file drives. Deliberately *not* `can-checkout`: `live-rules.spec.ts` owns that
 * one and normalises it around every test, and Playwright runs spec files on separate workers
 * concurrently — so sharing a rule between the two files would have each reverting the other's
 * document mid-flight. `fraud-screening` is touched by nothing else, and being an `AsyncRule` it
 * also proves the cascade reaches the async binder.
 */
const RULE = 'fraud-screening';

const ELIGIBLE = 'customer.e2e-eligible';
const BASE = 'customer.e2e-base';
const DERIVED = 'customer.e2e-derived';

/**
 * Inactive, but with orders on file: the two compiled specs this test swaps the proposition
 * between — `customer.has-orders` and `customer.is-active` — disagree about this customer, so the
 * screening verdict has to move when the proposition is redefined.
 */
const INACTIVE_WITH_ORDERS = '{ "age": 30, "isActive": false, "orderCount": 3 }';

async function ruleVersion(request: APIRequestContext, name: string): Promise<number> {
  const response = await request.get(`${API}/rules/${name}`);
  expect(response.ok()).toBe(true);
  return ((await response.json()) as { version: number }).version;
}

/**
 * Propositions and rules are per-process state on the running host, so normalise before AND after,
 * exactly as `live-rules.spec.ts` does: reverting works from any state and always moves the version
 * forward, so the run starts from the compiled defaults whatever an earlier run left behind.
 */
async function revertRule(request: APIRequestContext, name: string): Promise<number> {
  const response = await request.delete(`${API}/rules/${name}?baseVersion=${await ruleVersion(request, name)}`);
  expect(response.ok()).toBe(true);
  return ((await response.json()) as { version: number }).version;
}

/** Removes an authored proposition if one exists. Version 0 means "purely compiled": nothing to remove. */
async function removeProposition(request: APIRequestContext, name: string): Promise<void> {
  const response = await request.get(`${API}/propositions/${name}`);
  if (response.status() === 404) return;
  expect(response.ok()).toBe(true);
  const version = ((await response.json()) as { version: number }).version;
  if (version === 0) return;
  expect((await request.delete(`${API}/propositions/${name}?baseVersion=${version}`)).ok()).toBe(true);
}

/** POSTs a proposition that references one existing spec — the only shape the UI can author. */
async function authorProposition(
  request: APIRequestContext, name: string, startsFrom: string,
): Promise<void> {
  const response = await request.post(`${API}/propositions`, {
    data: { name, modelType: 'customer', document: { rule: { spec: startsFrom } }, description: null },
  });
  expect(response.ok()).toBe(true);
}

/**
 * The rule is reverted before the propositions are removed: a proposition a live rule still
 * references cannot be withdrawn, which is the very guarantee the third test asserts.
 */
async function normalise(request: APIRequestContext): Promise<void> {
  await revertRule(request, RULE);
  await removeProposition(request, DERIVED);
  await removeProposition(request, ELIGIBLE);
  await removeProposition(request, BASE);
}

/** Fills the New/Derive/Override dialog and submits it. Every flow must pick what it starts from. */
async function createFromDialog(
  page: Page, title: string, name: string, startsFrom: string,
): Promise<void> {
  const dialog = page.getByRole('dialog', { name: title });
  await dialog.getByLabel('Name', { exact: true }).fill(name);
  // UI-authored propositions are composition-only, so Create stays disabled until a source is
  // chosen. Picked explicitly rather than left on whichever name sorts first.
  await dialog.getByLabel('Starts from').selectOption(startsFrom);
  await dialog.getByRole('button', { name: 'Create' }).click();
  await expect(dialog).toBeHidden();
}

test.beforeEach(async ({ request }) => { await normalise(request); });
test.afterEach(async ({ request }) => { await normalise(request); });

test('an authored proposition is a building block the live rule follows', async ({ page, request }) => {
  // The feature's central claim, end to end: author a proposition, reference it from a rule, then
  // redefine the proposition and watch the running rule's verdict change without the rule being
  // saved again.
  const ruleBaseline = await ruleVersion(request, RULE);

  await page.goto('/#/propositions');

  // Author one over a compiled spec. Deliberately not `customer.is-active`, which is the shared
  // editor's seeded draft — a create that ignored the picker and cloned the draft would otherwise
  // produce the very document this asserts.
  await page.getByRole('button', { name: 'New', exact: true }).click();
  await createFromDialog(page, 'New proposition', ELIGIBLE, 'customer.has-orders');

  // It lands in the tree under its namespace, badged as authored rather than compiled…
  await expect(page.getByRole('treeitem', { name: 'e2e-eligible authored' })).toBeVisible();
  // …and its body is the reference the picker chose.
  await expect(page.getByLabel('rule document')).toContainText('"customer.has-orders"');

  // Reference it from the live rule. The rule is saved exactly once, here.
  await page.getByRole('tab', { name: 'Rules' }).click();
  await page.getByRole('combobox', { name: /^rule,/ }).click();
  await page.getByRole('option', { name: RULE }).click();
  await replaceBuffer(await openDsl(page), ELIGIBLE);
  await expect(page.getByLabel('rule document')).toContainText(`"${ELIGIBLE}"`);
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByText(new RegExp(`^v${ruleBaseline + 1}\\b`))).toBeVisible();

  // The running rule now decides through the proposition, and says so in the proposition's own
  // terms — the assertion text is the compiled spec's, which is what pins *which* spec it resolved to.
  const screening = page.locator('.verdict', { hasText: 'Screening (async rule)' });
  await page.getByRole('textbox', { name: 'customer', exact: true }).fill(INACTIVE_WITH_ORDERS);
  await page.getByRole('button', { name: 'Try checkout' }).click();
  await expect(screening).toContainText('customer has orders');

  // Redefine the proposition. The rule is never opened again.
  await page.getByRole('tab', { name: 'Propositions' }).click();
  await page.getByRole('treeitem', { name: 'e2e-eligible authored' }).click();
  await expect(page.getByLabel('rule document')).toContainText('"customer.has-orders"');

  // The blast radius is on the page before the edit is saved, not sprung afterwards.
  await expect(page.getByText('Changing this affects 1 rule:')).toBeVisible();
  await expect(page.getByRole('listitem').filter({ hasText: RULE })).toBeVisible();

  await replaceBuffer(await openDsl(page), 'customer.is-active');
  await expect(page.getByLabel('rule document')).toContainText('"customer.is-active"');
  // The count on the button is the same blast radius, restated where the commit happens.
  await page.getByRole('button', { name: 'Save (1)' }).click();
  await expect(page.getByText(/^v2\b/)).toBeVisible();

  // The verdict follows. Same rule document, same customer — a different answer.
  await page.getByRole('tab', { name: 'Rules' }).click();
  await page.getByRole('textbox', { name: 'customer', exact: true }).fill(INACTIVE_WITH_ORDERS);
  await page.getByRole('button', { name: 'Try checkout' }).click();
  await expect(screening).toContainText('customer is inactive');
  await expect(screening).not.toContainText('customer has orders');

  // And the rule really was untouched: still on the one version the UI saved.
  expect(await ruleVersion(request, RULE)).toBe(ruleBaseline + 1);
});

test('deriving from a proposition shows the blast radius on the one it was derived from', async ({ page }) => {
  await page.goto('/#/propositions');

  await page.getByRole('button', { name: 'New', exact: true }).click();
  await createFromDialog(page, 'New proposition', BASE, 'customer.is-active');
  await expect(page.getByRole('treeitem', { name: 'e2e-base authored' })).toBeVisible();

  // Derive seeds the source, so the dialog already knows what the new one starts from.
  await page.getByRole('button', { name: 'Derive from this' }).click();
  await createFromDialog(page, `Derive from ${BASE}`, DERIVED, BASE);
  await expect(page.getByRole('treeitem', { name: 'e2e-derived authored' })).toBeVisible();

  // Selecting the source shows what an edit to it would reach.
  await page.getByRole('treeitem', { name: 'e2e-base authored' }).click();
  await expect(page.getByText('Changing this affects 1 proposition:')).toBeVisible();
  await expect(page.getByRole('listitem').filter({ hasText: DERIVED })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Save (1)' })).toBeVisible();
});

test('a proposition something else references cannot be deleted', async ({ page, request }) => {
  await authorProposition(request, BASE, 'customer.is-active');
  await authorProposition(request, DERIVED, BASE);

  // Deep-linked: the dotted name is the route, so the selection survives a reload.
  await page.goto(`/#/propositions/${BASE}`);
  await expect(page.getByRole('button', { name: 'Save (1)' })).toBeVisible();

  await page.getByRole('button', { name: 'Delete', exact: true }).click();

  await expect(page.getByRole('alert')).toContainText(DERIVED);
  // Refused whole: the proposition is still there to select.
  await expect(page.getByRole('treeitem', { name: 'e2e-base authored' })).toBeVisible();
});
