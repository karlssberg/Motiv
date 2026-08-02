import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import { openDsl, replaceBuffer } from './dsl-surface.js';
import {
  chooseFromPalette, closePalette, expectDocument, expectInTree, openPalette, palette,
  paletteAction,
} from './shell.js';

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
  await paletteAction(page, 'New');
  await createFromDialog(page, 'New proposition', ELIGIBLE, 'customer.has-orders');

  // It lands in the tree under its namespace, badged as authored rather than compiled…
  await expectInTree(page, 'e2e-eligible authored');
  // …and its body is the reference the picker chose.
  await expectDocument(page, '"customer.has-orders"');

  // Reference it from the live rule. The rule is saved exactly once, here.
  await page.getByRole('tab', { name: 'Rules' }).click();
  await chooseFromPalette(page, 'Rules', RULE);
  await replaceBuffer(await openDsl(page), ELIGIBLE);
  await expectDocument(page, `"${ELIGIBLE}"`);
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
  await chooseFromPalette(page, 'Propositions', ELIGIBLE);
  await expectDocument(page, '"customer.has-orders"');

  // The blast radius is on the page before the edit is saved, not sprung afterwards.
  await expect(page.getByText('Changing this affects 1 rule:')).toBeVisible();
  await expect(page.getByRole('listitem').filter({ hasText: RULE })).toBeVisible();

  await replaceBuffer(await openDsl(page), 'customer.is-active');
  await expectDocument(page, '"customer.is-active"');
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

  await paletteAction(page, 'New');
  await createFromDialog(page, 'New proposition', BASE, 'customer.is-active');

  // One trip through the palette proves both: the new proposition is browsable in the tree under
  // its namespace, and Derive — aimed at the selection while the palette is browsing — seeds the
  // source, so the dialog already knows what the new one starts from.
  await openPalette(page, 'Propositions');
  await expect(palette(page, 'Propositions').getByRole('treeitem', { name: 'e2e-base authored' }))
    .toBeVisible();
  await palette(page, 'Propositions').getByRole('button', { name: 'Derive', exact: true }).click();
  await createFromDialog(page, `Derive from ${BASE}`, DERIVED, BASE);
  await expectInTree(page, 'e2e-derived authored');

  // Selecting the source shows what an edit to it would reach.
  await chooseFromPalette(page, 'Propositions', BASE);
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

  // Delete is a palette action, and the only one that leaves the palette standing — there is
  // nothing to go on to. Dismissed here so the banner underneath is reachable again.
  await paletteAction(page, 'Delete');
  await closePalette(page, 'Propositions');

  await expect(page.getByRole('alert')).toContainText(DERIVED);
  // Refused whole: the proposition is still there to select.
  await expectInTree(page, 'e2e-base authored');
});

/*
 * The three below are the only home for what `test/setup.ts`'s jsdom shim cannot model. That shim
 * sets `open` on `showModal()` and does nothing else — jsdom has no top layer — so focus trapping,
 * backdrop inertness, Escape, and geometry of any kind are unobservable to all 389 unit tests. A
 * browser is the only place they can be proven, and this is the only browser.
 */

test('the palette traps focus, closes on Escape, and makes the page behind it inert', async ({ page }) => {
  await page.goto('/#/propositions');
  await openPalette(page, 'Propositions');

  // Focus starts on the search box, and shift-tabbing *backwards* off it wraps round to the last
  // control in the dialog — Close — instead of reaching the page behind.
  //
  // Backwards on purpose. A forward Tab from the first control moves to the second whether or not
  // any trap exists, since the palette has a browse tree, four footer buttons and a Close after
  // this input; it would assert nothing, in the one test that exists to prove focus trapping.
  // Backwards there is nothing before the input to reach, so the wrap is the whole answer: with
  // the trap it comes round to Close, without one it walks into the toolbar behind.
  //
  // Two presses rather than one because Chromium routes the wrap through its own browser UI, and
  // for exactly one press `document.activeElement` reports `body` — focus is out of the document
  // but not yet back round. Measured, not assumed.
  await expect(palette(page, 'Propositions').getByRole('combobox')).toBeFocused();
  await page.keyboard.press('Shift+Tab');
  await page.keyboard.press('Shift+Tab');
  await expect(palette(page, 'Propositions').getByRole('button', { name: 'Close' })).toBeFocused();

  // The page behind is inert: the toolbar button that opened this cannot take focus back, which is
  // the guarantee `showModal()` makes and an `aria-modal` div never did.
  // Asserted through focus rather than visibility: Playwright's visibility is geometric, and a
  // measurement against this very button behind an open modal returned `true` — inert content is
  // still laid out, so `not.toBeVisible()` would have passed for no reason and failed to notice
  // if the modal stopped being modal. Not taking focus is the guarantee itself.
  const openButton = page.getByRole('button', { name: 'Open' });
  await openButton.evaluate((button) => (button as HTMLButtonElement).focus());
  const stillInside = await page.evaluate(() =>
    document.querySelector('dialog[open]')?.contains(document.activeElement) ?? false);
  expect(stillInside).toBe(true);

  await page.keyboard.press('Escape');
  await expect(palette(page, 'Propositions')).toBeHidden();
});

test('the palette fills the screen on a phone', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 780 });
  await page.goto('/#/propositions');
  await openPalette(page, 'Propositions');

  // jsdom computes no styles at all, so this is the only automated check on modal geometry that
  // exists: below 900px `modal-mobile-full` must beat every other rule that sets a max-width on
  // the same element, and all of them are single-class selectors — source order is the whole of
  // the cascade between them.
  const box = await palette(page, 'Propositions').boundingBox();
  expect(box?.width).toBe(390);
});

test('the document viewer opens from the toolbar on both pages', async ({ page }) => {
  await page.goto('/#/propositions');
  await page.getByRole('button', { name: 'JSON' }).click();
  await expect(page.getByRole('dialog', { name: /document/i })).toBeVisible();
  await page.keyboard.press('Escape');

  await page.goto('/#/rules');
  await page.getByRole('button', { name: 'JSON' }).click();
  await expect(page.getByRole('dialog', { name: /document/i })).toBeVisible();
});
