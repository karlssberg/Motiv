import type { Page } from '@playwright/test';
// `test` carries the API fixtures (and refuses a call it has none for) — see `stubs.ts`.
import { expect, test } from './stubs.js';

/**
 * The other mechanical half of ticket 18: the keyboard behaviour a *role* promises.
 *
 * axe cannot see any of this. A `role="tree"` whose rows are a column of tab stops, or a
 * `role="tablist"` with no panel to control, are approximations rather than violations — the
 * markup is well-formed and the names are present, so a scan reports green over a screen-reader
 * user who has been told to use arrow keys that do nothing. What catches that is driving the
 * keyboard, and it has to be a real browser: jsdom has no tab sequence of its own, so the unit
 * tests can assert which row *would* be reached and only this can assert that Tab reaches it.
 *
 * Here rather than in `e2e/` because it needs no backend — the same reason the axe sweep lives
 * here — so it runs on every pull request as part of the accessibility gate.
 */

/** The accessible name of whatever holds focus, as an assistive technology would announce it. */
async function focusedName(page: Page): Promise<string | null> {
  return page.evaluate(() => {
    const active = document.activeElement;
    return active === null ? null : active.getAttribute('aria-label') ?? active.textContent;
  });
}

/** Whether focus is inside the palette's namespace tree at all. */
async function insideTree(page: Page): Promise<boolean> {
  return page.evaluate(() =>
    document.querySelector('[role="tree"]')?.contains(document.activeElement) ?? false);
}

/** Open the propositions palette, which opens browsing its namespace tree. */
async function openPalette(page: Page): Promise<void> {
  await page.goto('/#/propositions');
  await expect(page.getByRole('link', { name: 'Rules' })).toBeVisible();
  await page.getByRole('button', { name: 'Open' }).click();
  await expect(page.getByRole('dialog', { name: 'Propositions' })).toBeVisible();
}

/** Tab forward until focus is in the tree, failing rather than looping if it never gets there. */
async function tabIntoTree(page: Page): Promise<number> {
  for (let stops = 1; stops <= 10; stops += 1) {
    await page.keyboard.press('Tab');
    if (await insideTree(page)) return stops;
  }
  throw new Error('Tab never reached the namespace tree.');
}

test.describe('the palette namespace tree honours role="tree"', () => {
  test('is one stop in the tab sequence, not one per proposition', async ({ page }) => {
    await openPalette(page);

    await tabIntoTree(page);
    expect(await insideTree(page)).toBe(true);

    // The next Tab is already past the whole tree: four propositions under two namespaces, and
    // crossing them costs one keystroke. Before, it cost one per row.
    await page.keyboard.press('Tab');
    expect(await insideTree(page)).toBe(false);
  });

  test('moves between rows on the arrow keys, and into and out of a subtree', async ({ page }) => {
    await openPalette(page);
    await tabIntoTree(page);

    // Entered at the first row, since nothing is selected on a fresh propositions page.
    expect(await focusedName(page)).toBe('customer');

    await page.keyboard.press('ArrowDown');
    expect(await focusedName(page)).toBe('is-active compiled');

    await page.keyboard.press('ArrowUp');
    expect(await focusedName(page)).toBe('customer');

    await page.keyboard.press('ArrowRight');
    expect(await focusedName(page)).toBe('is-active compiled');

    await page.keyboard.press('ArrowLeft');
    expect(await focusedName(page)).toBe('customer');

    await page.keyboard.press('End');
    expect(await focusedName(page)).toBe('is-large compiled');

    await page.keyboard.press('Home');
    expect(await focusedName(page)).toBe('customer');
  });

  test('jumps to a namespace by typing its first letters', async ({ page }) => {
    await openPalette(page);
    await tabIntoTree(page);

    await page.keyboard.press('o');

    expect(await focusedName(page)).toBe('orders');
  });

  test('chooses the focused proposition on Enter, so the palette is crossable by keyboard alone', async ({ page }) => {
    // The whole point of the pattern, end to end: open, walk, choose — no pointer anywhere.
    await openPalette(page);
    await tabIntoTree(page);

    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    expect(await focusedName(page)).toBe('is-adult compiled');
    await page.keyboard.press('Enter');

    await expect(page.getByRole('dialog', { name: 'Propositions' })).toBeHidden();
    expect(page.url()).toContain('#/propositions/customer.is-adult');
  });
});

test.describe('the page switcher is navigation', () => {
  test('offers links that say which page is current', async ({ page }) => {
    await page.goto('/#/propositions');

    const current = page.getByRole('link', { name: 'Propositions' });
    await expect(current).toHaveAttribute('aria-current', 'page');
    await expect(page.getByRole('link', { name: 'Rules' })).not.toHaveAttribute('aria-current', /.*/);
    // A destination, not a handler: it is the href that navigates, which is what makes
    // middle-click and open-in-new-tab work without anything arranging for them.
    await expect(page.getByRole('link', { name: 'Rules' })).toHaveAttribute('href', '#/rules');
  });

  test('navigates on Enter, the key a link is operated with', async ({ page }) => {
    await page.goto('/#/propositions');

    await page.getByRole('link', { name: 'Rules' }).focus();
    await page.keyboard.press('Enter');

    await expect(page.getByRole('link', { name: 'Rules' })).toHaveAttribute('aria-current', 'page');
    expect(page.url()).toContain('#/rules');
  });
});
