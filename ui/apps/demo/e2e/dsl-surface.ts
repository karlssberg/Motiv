import { expect, type Locator, type Page } from '@playwright/test';

/**
 * Driving the editor pane's DSL surface, shared by every spec that authors a document as text.
 * Not a `.spec.ts`, so Playwright's default `testMatch` leaves it alone and it is only ever
 * imported.
 */

/** Switches the editor pane to its DSL surface, returning the CodeMirror text element. */
export async function openDsl(page: Page): Promise<Locator> {
  // The builder's root row proves the live catalog loaded before the surface is switched.
  await expect(page.getByRole('button', { name: 'edit expression at $.rule' })).toBeVisible();
  await page.getByRole('tab', { name: 'DSL' }).click();

  const content = page.locator('.cm-content');
  await expect(content).toBeVisible();
  return content;
}

/** Replaces the whole buffer, the way a user retyping it would. */
export async function replaceBuffer(content: Locator, text: string): Promise<void> {
  await content.click();
  await content.page().keyboard.press('ControlOrMeta+a');
  await content.page().keyboard.type(text);
}
