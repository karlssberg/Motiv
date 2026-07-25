import { test, expect, type Locator, type Page } from '@playwright/test';

/** Switches the editor pane to its DSL surface, returning the CodeMirror text element. */
async function openDsl(page: Page): Promise<Locator> {
  // The builder's root leaf proves the live catalog loaded before the surface is switched.
  await expect(page.getByLabel('spec at $.rule')).toBeVisible();
  await page.getByRole('tab', { name: 'DSL' }).click();

  const content = page.locator('.cm-content');
  await expect(content).toBeVisible();
  return content;
}

/** Replaces the whole buffer, the way a user retyping it would. */
async function replaceBuffer(page: Page, text: string): Promise<void> {
  await page.locator('.cm-content').click();
  await page.keyboard.press('ControlOrMeta+a');
  await page.keyboard.type(text);
}

test('the DSL surface shows the rule as text, and editing the text drives the document', async ({ page }) => {
  await page.goto('/');
  const content = await openDsl(page);

  await expect(content).toHaveText('is-active');

  await replaceBuffer(page, 'is-active && is-adult');

  // The buffer debounce-commits into the store, so the JSON pane follows the text.
  // `&&` is the short-circuiting operator, so it prints as `andAlso`.
  const document = page.getByLabel('rule document');
  await expect(document).toContainText('"andAlso"');
  await expect(document).toContainText('"is-adult"');
});

test('an unknown spec is reported as a lint diagnostic', async ({ page }) => {
  await page.goto('/');
  await openDsl(page);

  await replaceBuffer(page, 'not-a-real-spec');

  // The diagnostic is a round trip: debounced commit, debounced POST /validate, the errors
  // written back onto the store, then CodeMirror's own (async) lint pass — well past the
  // default expect timeout.
  await expect(page.locator('.cm-lintRange-error').first()).toBeVisible({ timeout: 15_000 });
});

test('Format reprints the buffer canonically', async ({ page }) => {
  await page.goto('/');
  const content = await openDsl(page);

  await replaceBuffer(page, 'is-active     &&      is-adult');
  await page.getByRole('button', { name: 'Format' }).click();

  await expect(content).toHaveText('is-active && is-adult');
});
