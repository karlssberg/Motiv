import { test, expect } from '@playwright/test';
import { expectDocument, openScratchRule, scenarioRow } from './shell.js';

test('build a rule, then evaluate it end to end', async ({ page }) => {
  // A rule has to be open for there to be a builder: the page starts empty otherwise.
  await openScratchRule(page);

  // Build a composite by typing it: the row is where structure is authored.
  await page.getByRole('button', { name: 'edit expression at $.rule' }).click();
  await page.keyboard.press('ControlOrMeta+a');
  await page.keyboard.type('customer.is-active & customer.is-adult');
  await page.keyboard.press('Enter');

  // The document modal reflects the composite document.
  await expectDocument(page, '"and"');

  // Run the seeded scenarios: the Draft column is this document's verdict for each.
  await page.getByRole('button', { name: 'Run all' }).click();

  // An outcome is rendered (yes / no).
  await expect(scenarioRow(page).getByRole('cell', { name: 'draft' })).toHaveText(/yes|no/);
});
