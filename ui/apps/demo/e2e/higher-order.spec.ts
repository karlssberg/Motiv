import { test, expect } from '@playwright/test';

test('builds and evaluates a higher-order rule end to end', async ({ page }) => {
  await page.goto('/');

  // root leaf present (catalog loaded)
  await expect(page.getByLabel('spec at $.rule')).toBeVisible();

  // is-adult AND (is-active) AND (all orders are large)
  await page.getByLabel('spec at $.rule').selectOption('is-adult');
  await page.getByRole('button', { name: 'wrap $.rule in AND', exact: true }).click();
  await page.getByRole('button', { name: 'add quantifier to $.rule' }).click();

  // the document reflects the higher-order node over the orders collection
  await expect(page.getByLabel('rule document')).toContainText('asAllSatisfied');
  await expect(page.getByLabel('rule document')).toContainText('"path": "orders"');

  // a model whose orders are all large → asAllSatisfied is true → whole AND satisfied
  await page.getByLabel('sample model').fill(
    '{ "age": 30, "isActive": true, "orderCount": 2, "orders": [ { "total": 150 }, { "total": 200 } ] }',
  );
  await page.getByRole('button', { name: 'Evaluate' }).click();

  await expect(page.getByLabel('outcome')).toHaveText('Satisfied');
});

test('a mixed order set makes the quantifier — and the rule — not satisfied', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByLabel('spec at $.rule')).toBeVisible();

  await page.getByLabel('spec at $.rule').selectOption('is-adult');
  await page.getByRole('button', { name: 'wrap $.rule in AND', exact: true }).click();
  await page.getByRole('button', { name: 'add quantifier to $.rule' }).click();

  await page.getByLabel('sample model').fill(
    '{ "age": 30, "isActive": true, "orderCount": 2, "orders": [ { "total": 150 }, { "total": 40 } ] }',
  );
  await page.getByRole('button', { name: 'Evaluate' }).click();

  await expect(page.getByLabel('outcome')).toHaveText('Not satisfied');
});
