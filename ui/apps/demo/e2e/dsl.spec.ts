import { test, expect } from '@playwright/test';
import { openDsl, replaceBuffer } from './dsl-surface.js';
import { expectDocument } from './shell.js';

test('the DSL surface shows the rule as text, and editing the text drives the document', async ({ page }) => {
  await page.goto('/');
  const content = await openDsl(page);

  await expect(content).toHaveText('customer.is-active');

  await replaceBuffer(content, 'customer.is-active && customer.is-adult');

  // The buffer debounce-commits into the store, so the document follows the text.
  // `&&` is the short-circuiting operator, so it prints as `andAlso`.
  await expectDocument(page, '"andAlso"', '"customer.is-adult"');
});

test('an unknown spec is reported as a lint diagnostic', async ({ page }) => {
  await page.goto('/');
  const content = await openDsl(page);

  await replaceBuffer(content, 'not-a-real-spec');

  // The diagnostic is a round trip: the debounced commit, the debounced POST /validate, then
  // the errors written back onto the store — well past the default expect timeout.
  await expect(page.locator('.cm-lintRange-error').first()).toBeVisible({ timeout: 15_000 });
});

test('Format reprints the buffer canonically', async ({ page }) => {
  await page.goto('/');
  const content = await openDsl(page);

  await replaceBuffer(content, 'customer.is-active     &&      customer.is-adult');
  await page.getByRole('button', { name: 'Format' }).click();

  await expect(content).toHaveText('customer.is-active && customer.is-adult');
});
