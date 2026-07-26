import { test, expect } from '@playwright/test';

test('build a rule, then evaluate it end to end', async ({ page }) => {
  await page.goto('/');

  // Builder loaded from the live catalog: the root row renders its expression.
  await expect(page.getByRole('button', { name: 'edit expression at $.rule' })).toBeVisible();

  // Build a composite: wrap the root in AND (adds a second operand). The structural controls
  // live in the node's detail panel, which starts closed.
  await page.getByRole('button', { name: 'details for $.rule' }).click();
  await page.getByRole('button', { name: 'wrap $.rule in AND', exact: true }).click();

  // The JSON pane reflects the composite document.
  await expect(page.getByLabel('rule document')).toContainText('"and"');

  // Evaluate against the prefilled sample model.
  await page.getByRole('button', { name: 'Evaluate' }).click();

  // An outcome is rendered (Satisfied / Not satisfied).
  await expect(page.getByLabel('outcome')).toContainText(/Satisfied|Not satisfied/);
});
