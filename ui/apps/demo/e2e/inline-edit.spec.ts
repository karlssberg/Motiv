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
 * The caret is two controls sharing one slot — a leaf's detail toggle, a parent's subtree collapse
 * — and committing the row can turn the first into the second *between the press and the release*.
 *
 * Clicking the caret while an uncommitted buffer is open blurs the editor, which commits it; if
 * that commit gives the row children, the tree renders expanded and the caret becomes the collapse
 * toggle. React reused the same DOM node, so the click that followed ran the collapse it had never
 * been aimed at: the subtree appeared and vanished within one gesture, about 5ms apart. Only ever
 * on the first click, since a committed row's caret no longer changes meaning under the pointer.
 */
test('committing by clicking the caret leaves the new subtree revealed', async ({ page }) => {
  const row = await rootRow(page);
  await row.click();
  await page.keyboard.press('ControlOrMeta+a');
  // `insertText`, not `type`: a paste lands atomically and raises no completion popup, which is
  // both how this was reported and what keeps the caret click below the first commit of the row.
  await page.keyboard.insertText('is-active & (is-active | is-adult)');

  // Named by its accessible name rather than by class, which also asserts the premise: at the
  // moment of the press this row is still a leaf, so the caret is its detail toggle.
  await page.getByRole('button', { name: 'details for $.rule' }).hover();

  // Press and release as separate steps, with the commit awaited in between. A plain `click()` is
  // fast enough to release before React has re-rendered, so the click lands on the old caret and
  // is dropped for reasons that have nothing to do with this fix. Holding the button down until
  // the row has actually re-kinded is what makes the release land on the control the gesture was
  // never aimed at, every time.
  await page.mouse.down();
  await expect(page.getByLabel('rule document')).toContainText('"and"');
  await page.mouse.up();
  // The root, its two operands, and the nested or's two — the whole subtree, still on screen.
  await expect(page.locator('.node-row')).toHaveCount(5);
  // `exact`, or the name also matches the nested `collapse $.rule.and[1]` as a substring.
  await expect(page.getByRole('button', { name: 'collapse $.rule', exact: true }))
    .toHaveAttribute('aria-expanded', 'true');
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
