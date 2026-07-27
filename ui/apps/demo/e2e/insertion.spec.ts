import { test, expect, type Locator, type Page } from '@playwright/test';

/** Replaces the root row's expression by typing DSL into it, the way authoring works elsewhere. */
async function buildRootExpression(page: Page, dsl: string): Promise<void> {
  await expect(page.getByRole('button', { name: 'edit expression at $.rule' })).toBeVisible();
  await page.getByRole('button', { name: 'edit expression at $.rule' }).click();
  await page.keyboard.press('ControlOrMeta+a');
  await page.keyboard.type(dsl);
  await page.keyboard.press('Enter');
}

/** The phantom row's CodeMirror content element, opened by a row's `+`. */
function pendingContent(page: Page): Locator {
  return page.locator('.node-row-pending .cm-content');
}

/**
 * The `+` opens a brand-new CodeMirror instance on a row that did not exist a moment ago. jsdom
 * focuses hidden elements regardless of visibility or mount timing, so a unit test asserting focus
 * there would pass whether or not the real editor actually took it — only a browser proves it.
 */
test('the phantom editor opened by + receives focus', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'edit expression at $.rule' })).toBeVisible();

  await page.getByRole('button', { name: 'insert after $.rule', exact: true }).click();

  await expect(pendingContent(page)).toBeFocused();
});

/**
 * jsdom implements no layout at all, so `scrollIntoView` is stubbed to a no-op in `test/setup.ts`
 * for the unit suite — whether the strip actually scrolls a mark into view is unobservable there.
 * This builds a rule long enough that the strip overflows, hovers the row whose span sits off the
 * right edge, and asserts the strip's horizontal scroll position actually moved.
 */
test('the strip scrolls the hovered mark into view', async ({ page }) => {
  await page.goto('/');

  // Alternating the two customer specs keeps the DSL valid while making it long enough — twenty
  // repeats prints well over a thousand characters, far wider than the strip's card.
  const longExpression = Array.from({ length: 20 }, (_, i) => (i % 2 === 0 ? 'is-active' : 'is-adult'))
    .join(' & ');
  await buildRootExpression(page, longExpression);
  await expect(page.getByLabel('rule document')).toContainText('"and"');

  const strip = page.locator('.dsl-strip');
  // Building the expression can itself leave a row hovered (the cursor rests over whatever
  // ends up under it once the row re-renders from leaf to composite), so the strip may already
  // have nudged its scroll position once before this point. Moving off the tree gives a clean,
  // settled baseline to compare against, rather than assuming that baseline is literally 0.
  await page.mouse.move(0, 0);
  const before = await strip.evaluate((el) => el.scrollLeft);

  // The last operand prints at the tail of the flattened text, off the right edge from `before`.
  await page.locator('.node-row').last().hover();

  await expect.poll(() => strip.evaluate((el) => el.scrollLeft)).toBeGreaterThan(before);
});

/**
 * Proves the planner's output is expressible DSL, and that normalization reached the JSON the DSL
 * pane renders from — a round trip through the builder's insertion UI and back out as text.
 *
 * This currently fails in a real browser. Pressing Enter to commit runs `useInlineDslEditor`'s
 * `onCommit` (apps/demo/src/builder/useInlineDslEditor.ts) twice, not once: the Enter keymap
 * handler commits and, synchronously in the same event, React removes the phantom row's DOM —
 * which fires a genuine native `blur` on the still-focused CodeMirror content element before the
 * paint. `EditorView.domEventHandlers.blur` also commits, and its guard (`attached.current`,
 * meant to make a post-teardown commit a no-op) is only cleared inside the mount effect's
 * *cleanup*, which — because the effect is a plain `useEffect`, not a `useLayoutEffect` — runs as
 * a deferred passive effect, later than this synchronous blur. So the guard is still `true` when
 * the second commit lands.
 *
 * This double-commit is not specific to `PendingSlot`: the same two-`onCommit` sequence fires from
 * `NodeDsl` (apps/demo/src/builder/NodeDsl.tsx) on every row edit — confirmed by instrumenting it
 * directly. It is invisible there because `store.replaceNode(path, sameResult)` twice is a no-op;
 * `PendingSlot`'s `store.applyPlan(planInsert(...))` (apps/demo/src/builder/RuleNodeEditor.tsx,
 * `slotFor`'s `onCommit`) is not idempotent, so committing an insertion inserts the node twice.
 * None of the 242 jsdom tests catch it because jsdom does not reliably fire a synchronous native
 * `blur` when a focused element is removed from the document — precisely the class of bug this
 * milestone's e2e suite exists to catch. `test.fail()` documents it as a known, reproducible
 * defect rather than papering over it — this spec should start passing, unprompted, the day the
 * guard is fixed (e.g. by clearing `attached.current` synchronously, such as from the commit
 * itself, rather than relying on the passive effect cleanup).
 */
test('inserting an operand round-trips into the DSL pane', async ({ page }) => {
  test.fail(true, 'useInlineDslEditor double-commits on Enter — see comment above');
  await page.goto('/');
  await buildRootExpression(page, 'is-active & is-adult');
  await expect(page.getByLabel('rule document')).toContainText('"and"');

  await page.getByRole('button', { name: 'insert after $.rule.and[0]', exact: true }).click();
  await expect(pendingContent(page)).toBeFocused();
  await page.keyboard.type('has-orders');
  await page.keyboard.press('Enter');

  // The phantom row is gone once the commit lands — the insertion applied to the document.
  await expect(page.locator('.node-row-pending')).toHaveCount(0);
  await expect(page.getByLabel('rule document')).toContainText('has-orders');

  await page.getByRole('tab', { name: 'DSL' }).click();
  const content = page.locator('.cm-content');
  await expect(content).toHaveText('is-active & has-orders & is-adult');
});
