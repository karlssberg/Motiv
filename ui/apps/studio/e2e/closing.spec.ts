import { test, expect } from '@playwright/test';
import { closeTab, openScratchRule } from './shell.js';

/**
 * Opening, closing and saving-and-closing a document's tab. jsdom proves the wiring; a browser is
 * where the `<dialog>` actually traps focus and where the route round-trips.
 */

test('closing the last clean tab returns to the empty state, and the route forgets it', async ({ page }) => {
  await page.goto('/#/rules/can-checkout');
  await expect(page.getByRole('region', { name: 'Editor' })).toBeVisible();

  await closeTab(page, 'can-checkout');

  await expect(page.getByRole('region', { name: 'Nothing open' })).toBeVisible();
  expect(page.url()).toMatch(/#\/rules$/);
  // The empty state's own chooser is the strip's palette.
  await page.getByRole('button', { name: 'Open a rule or proposition' }).click();
  await expect(page.getByRole('dialog', { name: 'Open' })).toBeVisible();
});

test('closing a rule with unsaved changes asks first, and Keep editing keeps it', async ({ page }) => {
  await openScratchRule(page);
  await page.getByRole('button', { name: 'edit expression at $.rule' }).click();
  await page.keyboard.press('ControlOrMeta+a');
  await page.keyboard.type('!customer.is-active');
  await page.keyboard.press('Enter');

  await closeTab(page, 'can-checkout');
  const dialog = page.getByRole('dialog', { name: 'Unsaved changes' });
  await expect(dialog).toBeVisible();
  // The safe answer holds focus when the dialog opens — a stray Enter keeps, never discards.
  await expect(dialog.getByRole('button', { name: 'Keep editing' })).toBeFocused();

  await dialog.getByRole('button', { name: 'Keep editing' }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole('region', { name: 'Editor' })).toBeVisible();

  await closeTab(page, 'can-checkout');
  await page.getByRole('dialog', { name: 'Unsaved changes' }).getByRole('button', { name: 'Discard changes' }).click();
  await expect(page.getByRole('region', { name: 'Nothing open' })).toBeVisible();
});

test('the save menu offers Save & close, and remembers it as the default', async ({ page }) => {
  // On a rule Save is available, so its menu is too: `customer.is-active` is served by a compiled
  // spec and has nothing to save.
  await page.goto('/#/rules/can-checkout');
  await expect(page.getByRole('region', { name: 'Editor' })).toBeVisible();

  const toggle = page.getByRole('button', { name: 'Save options' });
  await toggle.click();
  const menu = page.getByRole('menu', { name: 'Save options' });
  await expect(menu).toBeVisible();
  // Opening lands on the variant in force.
  await expect(menu.locator('[role="menuitemradio"][aria-checked="true"]')).toBeFocused();
  await page.keyboard.press('ArrowDown');
  await expect(menu.getByRole('menuitemradio', { name: /Save & close/ })).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(menu).toBeHidden();
  await expect(toggle).toBeFocused();

  // The remembered default carries across a fresh page: nothing is saved here, the preference is.
  await page.evaluate(() => window.localStorage.setItem('motiv.studio.save-default', 'save-close'));
  await page.reload();
  await expect(page.getByRole('button', { name: 'Save & close', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Save & close options' })).toBeVisible();
});
