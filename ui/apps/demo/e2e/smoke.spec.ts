import { test, expect } from '@playwright/test';
import { expectDocument } from './shell.js';

test('build a rule, then evaluate it end to end', async ({ page }) => {
  await page.goto('/');

  // Builder loaded from the live catalog: the root row renders its expression.
  await expect(page.getByRole('button', { name: 'edit expression at $.rule' })).toBeVisible();

  // Build a composite by typing it: the row is where structure is authored.
  await page.getByRole('button', { name: 'edit expression at $.rule' }).click();
  await page.keyboard.press('ControlOrMeta+a');
  await page.keyboard.type('customer.is-active & customer.is-adult');
  await page.keyboard.press('Enter');

  // The document modal reflects the composite document.
  await expectDocument(page, '"and"');

  // Evaluate against the prefilled sample model.
  await page.getByRole('button', { name: 'Evaluate' }).click();

  // An outcome is rendered (Satisfied / Not satisfied).
  await expect(page.getByLabel('outcome')).toContainText(/Satisfied|Not satisfied/);
});
