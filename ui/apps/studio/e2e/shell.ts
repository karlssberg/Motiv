import { expect, type Locator, type Page } from '@playwright/test';

/**
 * Driving the shell — the tab strip, the palettes, and the modals behind them — shared by every
 * spec that has to get at something the chrome now hides.
 *
 * Not a `.spec.ts`, so Playwright's default `testMatch` leaves it alone and it is only ever
 * imported.
 */

/**
 * The two palettes, named for what a spec reaches for. `Rules` is the shell's one **Open**
 * palette — rules and propositions together, whose dialog is named "Open" — and `Propositions`
 * is the explorer one step in from it, behind *Manage propositions*, which keeps its own name
 * and its authoring footer (New / Derive / Override / Delete).
 */
export type PaletteName = 'Propositions' | 'Rules';

/** The dialog's accessible name for a palette. */
function dialogName(name: PaletteName): string {
  return name === 'Rules' ? 'Open' : 'Propositions';
}

/**
 * The open palette, as a locator to scope queries to.
 *
 * Scoping matters rather more than it looks: `role="combobox"` is also what the builder's operator
 * badges carry, and `role="option"` what their listboxes carry, so an unscoped `getByRole` would
 * be a strict-mode violation the moment the document under edit has an operator in it. Everything
 * below reaches through here for that reason.
 */
export function palette(page: Page, name: PaletteName): Locator {
  return page.getByRole('dialog', { name: dialogName(name) });
}

/**
 * Open the palette and wait for it to be ready to type into. The strip's **Open** (also ⌘K) opens
 * the Open palette; the explorer is one more click, from its footer.
 */
export async function openPalette(page: Page, name: PaletteName): Promise<void> {
  await page.getByRole('button', { name: 'Open', exact: true }).click();
  await expect(page.getByRole('dialog', { name: 'Open' })).toBeVisible();
  if (name === 'Propositions') {
    await page.getByRole('button', { name: 'Manage propositions' }).click();
  }
  await expect(palette(page, name)).toBeVisible();
}

/** Dismiss the palette without choosing, the way Escape does it for real. */
export async function closePalette(page: Page, name: PaletteName): Promise<void> {
  await page.keyboard.press('Escape');
  await expect(palette(page, name)).toBeHidden();
}

/**
 * Open the palette, filter to `target`, and choose it.
 *
 * Matched on the row's text rather than its accessible name. A proposition row renders its
 * namespace, its leaf and its origin badge as three flex items, and accessible-name computation
 * inserts a space between anything that is not inline — so `customer.e2e-base` is *named*
 * "customer. e2e-base authored" and a name match on the dotted string finds nothing. The text
 * content has no such gaps.
 */
export async function chooseFromPalette(
  page: Page, name: PaletteName, target: string,
): Promise<void> {
  await openPalette(page, name);
  await palette(page, name).getByRole('combobox').fill(target);
  await palette(page, name).getByRole('option').filter({ hasText: target }).first().click();
  await expect(palette(page, name)).toBeHidden();
  // The choice is a navigation, and the tab follows the route a frame later: until it has, the
  // previous document's panel is still the visible one, and a query made now would land on it.
  const escaped = target.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  await expect(page.getByRole('tab', { name: new RegExp(`${escaped}$`), selected: true })).toBeVisible();
  await expect(page.getByRole('tabpanel', { name: target })).toBeVisible();
}

/**
 * Open the palette and take one of the footer's actions.
 *
 * The footer aims at the highlighted row while one exists and at the current selection otherwise —
 * and browsing (an empty query, which is how the palette opens) highlights nothing on purpose. So
 * an action taken straight after opening is an action on whatever the page already has loaded.
 *
 * Deliberately does not wait for the palette to close: New, Derive and Override dismiss it on the
 * way into their dialog, and Delete leaves it standing.
 */
export async function paletteAction(page: Page, action: string): Promise<void> {
  await openPalette(page, 'Propositions');
  await palette(page, 'Propositions').getByRole('button', { name: action, exact: true }).click();
}

/**
 * Assert a proposition is browsable in the palette's namespace tree, by the accessible name that
 * carries its badges — then leave the palette as it was found.
 */
export async function expectInTree(page: Page, name: string): Promise<void> {
  await openPalette(page, 'Propositions');
  await expect(palette(page, 'Propositions').getByRole('treeitem', { name })).toBeVisible();
  await closePalette(page, 'Propositions');
}

/**
 * Open the document modal, returning the `<pre>` the JSON is rendered into.
 *
 * The JSON pane used to sit beside the editor and could simply be read; it is a modal behind the
 * toolbar's third icon now, so every look at the document is an open and a close. Use
 * {@link expectDocument} unless the assertion needs the element itself — a `not`, or a count.
 */
export async function openDocument(page: Page): Promise<Locator> {
  await page.getByRole('button', { name: 'JSON' }).click();
  const document = page.getByLabel('rule document');
  await expect(document).toBeVisible();
  return document;
}

/** Dismiss the document modal, so the page beneath it is reachable again. */
export async function closeDocument(page: Page): Promise<void> {
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog', { name: 'Document' })).toBeHidden();
}

/** Open the document modal, assert every fragment appears in it, and close it again. */
export async function expectDocument(
  page: Page, ...fragments: Array<string | RegExp>
): Promise<void> {
  const document = await openDocument(page);
  for (const fragment of fragments) await expect(document).toContainText(fragment);
  await closeDocument(page);
}

/** The chip for an open document, by the name its tab carries. */
export function tab(page: Page, kind: 'Rule' | 'Proposition', name: string): Locator {
  return page.getByRole('tab', { name: `${kind} ${name}` });
}

/** Close a document's tab from its ×. */
export async function closeTab(page: Page, name: string): Promise<void> {
  // The × is a pointer affordance outside the accessibility tree (see TabStrip), so by attribute.
  await page.locator(`.chip-close[aria-label^="Close ${name}"]`).click();
}

/**
 * A rule to edit, opened fresh and normalised: the live `can-checkout`, with its root expression
 * typed back to `customer.is-active`.
 *
 * There is no local draft — the shell shows an empty state until a document is in the route — so
 * a spec that exercises the builder has to open one. `can-checkout` is a code-defined default, so
 * what the editor shows is the builder's seed rather than anything fetched; and because another
 * spec may be mid-save on the same rule in a parallel worker, the root is set explicitly rather
 * than assumed. Nothing here is saved, so the server is untouched.
 */
export async function openScratchRule(page: Page): Promise<void> {
  await page.goto('/#/rules/can-checkout');
  const root = page.getByRole('button', { name: 'edit expression at $.rule' });
  await expect(root).toBeVisible();
  await root.click();
  await page.keyboard.press('ControlOrMeta+a');
  await page.keyboard.type('customer.is-active');
  await page.keyboard.press('Enter');
  await expect(root).toHaveText('customer.is-active');
}

/** The seeded scenario every builder spec runs against; the first row of the Evaluate table. */
export const FIRST_SCENARIO = 'Active adult, 3 orders';

/** The Evaluate table's row for one named scenario. */
export function scenarioRow(page: Page, name: string = FIRST_SCENARIO): Locator {
  return page.getByRole('row', { name, exact: true });
}

/**
 * Re-models a seeded scenario and runs the table, then returns the row's Draft cell — what the
 * tab's document decides for that model. Opens the row's detail to reach its editor.
 */
export async function evaluateDraft(page: Page, model: string, name: string = FIRST_SCENARIO): Promise<Locator> {
  const row = scenarioRow(page, name);
  await row.getByRole('button', { name: `details of ${name}` }).click();
  await page.getByRole('textbox', { name: 'scenario model' }).fill(model);
  await page.getByRole('button', { name: 'Run all' }).click();
  return row.getByRole('cell', { name: 'draft' });
}
