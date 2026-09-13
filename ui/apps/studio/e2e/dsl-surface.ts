import { expect, type Locator, type Page } from '@playwright/test';

/**
 * Driving the editor pane's DSL surface, shared by every spec that authors a document as text.
 * Not a `.spec.ts`, so Playwright's default `testMatch` leaves it alone and it is only ever
 * imported.
 */

/** Switches the editor pane to its DSL surface, returning the CodeMirror text element. */
export async function openDsl(page: Page): Promise<Locator> {
  // The builder's root row proves the live catalog loaded before the surface is switched.
  // Scoped to the panel in front: every open tab keeps its editor mounted, hidden.
  const panel = page.locator('.tab-panel:not([hidden])');
  await expect(panel.getByRole('button', { name: 'edit expression at $.rule' })).toBeVisible();
  await panel.getByRole('tab', { name: 'DSL' }).click();

  // Scoped to the panel in front: every open tab keeps its editor mounted, hidden, and a class
  // locator — unlike a role query — does not skip hidden content.
  const content = page.locator('.tab-panel:not([hidden]) .cm-content');
  await expect(content).toBeVisible();
  return content;
}

/** Replaces the whole buffer, the way a user retyping it would. */
export async function replaceBuffer(content: Locator, text: string): Promise<void> {
  await content.click();
  await content.page().keyboard.press('ControlOrMeta+a');
  await content.page().keyboard.type(text);
}
