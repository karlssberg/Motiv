import { test, expect, type Locator, type Page } from '@playwright/test';

/** The root row, once the live catalog has loaded and rendered its expression. */
async function rootRow(page: Page): Promise<Locator> {
  await page.goto('/');
  const row = page.getByRole('button', { name: 'edit expression at $.rule' });
  await expect(row).toHaveText('is-active');
  return row;
}

/** The editor a row swaps itself for once focused. */
function rowEditor(page: Page): Locator {
  return page.locator('.node-dsl-host .cm-content');
}

/**
 * What a row's text actually measures at, read the same way in both of its states.
 *
 * A `Range` over the element's contents, rather than the element's own box: the read state is a
 * `flex: 1` button and the edit state a full-width line, so both boxes span the row regardless of
 * how much text is in them. Only the glyphs' own rectangle says whether the text moved.
 */
async function textMetrics(target: Locator): Promise<{ left: number; top: number; fontSize: string }> {
  return target.evaluate((element) => {
    const range = document.createRange();
    range.selectNodeContents(element);
    const box = range.getBoundingClientRect();
    return { left: box.left, top: box.top, fontSize: getComputedStyle(element).fontSize };
  });
}

/**
 * The row's text must not move when it is focused for editing. Two things used to move it, and
 * both are invisible to jsdom: CodeMirror's base theme indents every line by 6px
 * (`.cm-line { padding: 0 2px 0 6px }`), and `motivEditorTheme` — sized for the full DSL pane —
 * sets a 13.5px font the row's own 12.5px static text does not use. Together the expression
 * jumped right and grew the moment you clicked it.
 */
test('focusing a row for editing leaves its text exactly where it was', async ({ page }) => {
  const row = await rootRow(page);

  const before = await textMetrics(row);
  await row.click();

  const line = page.locator('.node-dsl-host .cm-line');
  await expect(line).toBeVisible();
  const after = await textMetrics(line);

  // Sub-pixel tolerance only: this guards against the 6px indent and the 1px type-size bump,
  // not against fractional layout noise.
  expect(after.left).toBeCloseTo(before.left, 1);
  expect(after.top).toBeCloseTo(before.top, 1);
  expect(after.fontSize).toBe(before.fontSize);
});

/**
 * A click carries a point, and the row is a line of text — so the point means "put the caret
 * here", exactly as it does in every other text field. The editor used to select the whole buffer
 * on mount regardless of how it was entered, so clicking into the middle of an expression to fix
 * one spec name armed the next keystroke to replace the entire line instead.
 *
 * Asserted by typing rather than by reading the selection: where the caret *is* is only meaningful
 * as where the next character *lands*, and that is what the user experiences.
 */
test('clicking a row places the caret where you clicked', async ({ page }) => {
  const row = await rootRow(page);

  // The boundary between "is-" and "active", measured on the static text — the coordinate a user
  // aiming at that gap would click, not a position derived from the editor that replaces it.
  const gap = await row.locator('.tok-spec').evaluate((element, upTo: number) => {
    const range = document.createRange();
    range.setStart(element.firstChild!, 0);
    range.setEnd(element.firstChild!, upTo);
    const box = range.getBoundingClientRect();
    return { x: box.right, y: box.top + box.height / 2 };
  }, 'is-'.length);
  await page.mouse.click(gap.x, gap.y);

  await expect(rowEditor(page)).toBeFocused();
  await page.keyboard.type('X');
  await expect(rowEditor(page)).toHaveText('is-Xactive');
});

/**
 * The keyboard has no point to aim at, so Tab keeps the select-all that makes the next keystroke
 * replace the expression. The two entry paths are tested together because the click fix is only
 * correct if it left this one alone.
 */
test('tabbing into a row still selects the whole expression', async ({ page }) => {
  await (await rootRow(page)).focus();

  await expect(rowEditor(page)).toBeFocused();
  await page.keyboard.type('X');
  await expect(rowEditor(page)).toHaveText('X');
});
