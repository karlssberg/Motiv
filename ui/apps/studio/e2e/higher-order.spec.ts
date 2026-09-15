import { test, expect } from '@playwright/test';
import type { Page } from '@playwright/test';
import { evaluateDraft, expectDocument, openScratchRule } from './shell.js';

/** Replaces a row's expression by typing DSL into it, the way authoring now works. */
async function typeExpression(page: Page, path: string, dsl: string): Promise<void> {
  await page.getByRole('button', { name: `edit expression at ${path}` }).click();
  await page.keyboard.press('ControlOrMeta+a');
  await page.keyboard.type(dsl);
  await page.keyboard.press('Enter');
}

/** Builds `customer.is-adult & all in orders { order.is-large }` through the builder's rows. */
async function buildHigherOrderRule(page: Page): Promise<void> {
  await openScratchRule(page);

  // root row present (catalog loaded)
  await expect(page.getByRole('button', { name: 'edit expression at $.rule' })).toBeVisible();

  await typeExpression(page, '$.rule', 'customer.is-adult & all in orders { order.is-large }');

  // the document reflects the higher-order node over the orders collection
  await expectDocument(page, 'asAllSatisfied', '"path": "orders"');
}

test('builds and evaluates a higher-order rule end to end', async ({ page }) => {
  await buildHigherOrderRule(page);

  // a model whose orders are all large → asAllSatisfied is true → whole AND satisfied
  const draft = await evaluateDraft(page,
    '{ "age": 30, "isActive": true, "orderCount": 2, "orders": [ { "total": 150 }, { "total": 200 } ] }');

  await expect(draft).toHaveText(/yes/);
});

test('a mixed order set makes the quantifier — and the rule — not satisfied', async ({ page }) => {
  await buildHigherOrderRule(page);

  const draft = await evaluateDraft(page,
    '{ "age": 30, "isActive": true, "orderCount": 2, "orders": [ { "total": 150 }, { "total": 40 } ] }');

  await expect(draft).toHaveText(/no/);
});
