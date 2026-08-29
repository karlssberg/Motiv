import { expect, type Locator, type Page } from '@playwright/test';

/**
 * Driving the minimalist shell — the three-icon toolbar, the command palette, and the modals
 * behind them — shared by every spec that has to get at something the chrome now hides.
 *
 * Not a `.spec.ts`, so Playwright's default `testMatch` leaves it alone and it is only ever
 * imported.
 */

/** The two palettes, named for what they list. The name is also the dialog's accessible name. */
export type PaletteName = 'Propositions' | 'Rules';

/**
 * The open palette, as a locator to scope queries to.
 *
 * Scoping matters rather more than it looks: `role="combobox"` is also what the builder's operator
 * badges carry, and `role="option"` what their listboxes carry, so an unscoped `getByRole` would
 * be a strict-mode violation the moment the document under edit has an operator in it. Everything
 * below reaches through here for that reason.
 */
export function palette(page: Page, name: PaletteName): Locator {
  return page.getByRole('dialog', { name });
}

/** Open the palette and wait for it to be ready to type into. */
export async function openPalette(page: Page, name: PaletteName): Promise<void> {
  await page.getByRole('button', { name: 'Open' }).click();
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
