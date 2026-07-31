import { test, expect } from '@playwright/test';

/**
 * The operator badge is a custom control, so the behaviour a native `<select>` would have supplied
 * is ours to provide — and ours to get wrong in a real browser in ways jsdom cannot see. Focus
 * moving into a card that is still `visibility: hidden` is exactly that class of bug: silent, and
 * invisible to the unit tests.
 */
test('an operator can be changed from the keyboard alone', async ({ page }) => {
  await page.goto('/');

  await page.getByRole('button', { name: 'edit expression at $.rule' }).click();
  await page.keyboard.press('ControlOrMeta+a');
  await page.keyboard.type('customer.is-active & customer.is-adult');
  await page.keyboard.press('Enter');

  const badge = page.getByRole('combobox', { name: /^operator at \$\.rule/ });
  await expect(badge).toHaveText('AND');

  // Opening lands on the operator in force, so the list starts where the value is.
  await badge.press('Enter');
  // `exact`, or the name matches `AndAlso` as a substring too.
  await expect(page.getByRole('option', { name: 'AND', exact: true })).toBeFocused();

  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await expect(page.getByRole('option', { name: 'XOR' })).toBeFocused();
  await page.keyboard.press('Enter');

  await expect(page.getByRole('listbox')).toHaveCount(0);
  await expect(badge).toHaveText('XOR');
  await expect(badge).toBeFocused();
  await expect(page.getByLabel('rule document')).toContainText('"xor"');
});
