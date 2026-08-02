import { test, expect } from '@playwright/test';
import type { Page } from '@playwright/test';

/** Replaces a row's expression by typing DSL into it, the way authoring now works. */
async function typeExpression(page: Page, path: string, dsl: string): Promise<void> {
  await page.getByRole('button', { name: `edit expression at ${path}` }).click();
  await page.keyboard.press('ControlOrMeta+a');
  await page.keyboard.type(dsl);
  await page.keyboard.press('Enter');
}

/** Builds `customer.is-adult & all in orders { order.is-large }` through the builder's rows. */
async function buildHigherOrderRule(page: Page): Promise<void> {
  await page.goto('/');

  // root row present (catalog loaded)
  await expect(page.getByRole('button', { name: 'edit expression at $.rule' })).toBeVisible();

  await typeExpression(page, '$.rule', 'customer.is-adult & all in orders { order.is-large }');

  // the document reflects the higher-order node over the orders collection
  await expect(page.getByLabel('rule document')).toContainText('asAllSatisfied');
  await expect(page.getByLabel('rule document')).toContainText('"path": "orders"');
}

test('builds and evaluates a higher-order rule end to end', async ({ page }) => {
  await buildHigherOrderRule(page);

  // a model whose orders are all large → asAllSatisfied is true → whole AND satisfied
  await page.getByLabel('sample model').fill(
    '{ "age": 30, "isActive": true, "orderCount": 2, "orders": [ { "total": 150 }, { "total": 200 } ] }',
  );
  await page.getByRole('button', { name: 'Evaluate' }).click();

  await expect(page.getByLabel('outcome')).toHaveText('Satisfied');
});

test('a mixed order set makes the quantifier — and the rule — not satisfied', async ({ page }) => {
  await buildHigherOrderRule(page);

  await page.getByLabel('sample model').fill(
    '{ "age": 30, "isActive": true, "orderCount": 2, "orders": [ { "total": 150 }, { "total": 40 } ] }',
  );
  await page.getByRole('button', { name: 'Evaluate' }).click();

  await expect(page.getByLabel('outcome')).toHaveText('Not satisfied');
});
