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
 * This test is also the regression guard for a bug it found: committing via Enter used to run
 * `useInlineDslEditor`'s `onCommit` (apps/demo/src/builder/useInlineDslEditor.ts) twice. The Enter
 * keymap handler committed and, synchronously in the same event, React removed the phantom row's
 * DOM — which fires a genuine native `blur` on the still-focused CodeMirror content element before
 * paint. `EditorView.domEventHandlers.blur` also committed, and its guard (`attached.current`,
 * meant to make a post-teardown commit a no-op) used to be cleared only inside the mount effect's
 * *cleanup*, which — because the effect is a plain `useEffect`, not a `useLayoutEffect` — runs as a
 * deferred passive effect, later than this synchronous blur. So the guard was still `true` when the
 * second commit landed, inserting the node twice (`is-active & has-orders & has-orders & is-adult`).
 * `commit` now disarms the guard itself, before delegating, rather than relying on effect-cleanup
 * timing — see `useInlineDslEditor.ts` for the fix. No jsdom test caught the original bug because
 * jsdom does not reliably fire a synchronous native `blur` when a focused element is removed from
 * the document — precisely the class of bug this milestone's e2e suite exists to catch.
 */
test('inserting an operand round-trips into the DSL pane', async ({ page }) => {
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

/**
 * The sibling regression guard: cancelling must not commit either. Before the fix, Escape called
 * `options.onCancel` directly, bypassing the guard entirely — so the same teardown-blur mechanism
 * described above re-entered `commit` with the guard still armed and the typed (but never
 * committed) buffer still sitting in the doc, silently inserting the very node the user just
 * cancelled. `cancel()` in `useInlineDslEditor.ts` now shares the same guard as `commit()`.
 *
 * Escape is pressed twice: a fully-typed spec name like `has-orders` is also a live completion
 * match, and CodeMirror's own autocomplete extension claims the first Escape to dismiss that
 * completion state (no visible popup renders in that same tick, so nothing else in this suite
 * observes it) — a pre-existing, unrelated characteristic of the shared editor hook, not something
 * introduced by either bug. The second Escape reaches this editor's own cancel binding, which is
 * the one under test. Confirmed deterministic (unaffected by the completion dismissal) by running
 * this exact sequence repeatedly against both the buggy and fixed code during development.
 */
/**
 * Blur is the most common real dismissal — clicking anywhere else while the phantom editor is
 * focused — and jsdom cannot exercise it: it does not reliably fire a synchronous native `blur`
 * when a focused element loses focus by other means, which is exactly why the unit suite drives
 * `fireEvent.blur` directly rather than relying on a click to produce it (see
 * `apps/demo/test/builder/NodeInsert.test.tsx`). It is also the path that produced the duplicate-
 * insertion bug the previous test guards. Only a real browser proves a click-away commits exactly
 * once — not twice, and not zero times.
 */
test('blurring a parseable slot by clicking elsewhere commits it exactly once', async ({ page }) => {
  await page.goto('/');
  await buildRootExpression(page, 'is-active & is-adult');
  await expect(page.getByLabel('rule document')).toContainText('"and"');

  await page.getByRole('button', { name: 'insert after $.rule.and[0]', exact: true }).click();
  await expect(pendingContent(page)).toBeFocused();
  await page.keyboard.type('has-orders');

  // A neutral area of the page — the DSL strip's inert "rule" caption — to blur the editor
  // without triggering any other row's own insertion or edit affordance.
  await page.locator('.dsl-strip-label').click();

  await expect(page.locator('.node-row-pending')).toHaveCount(0);
  const document = page.getByLabel('rule document');
  await expect(document).toContainText('has-orders');
  // Exactly one insertion: not the pre-fix double-commit, and not a silently dropped one. A
  // `text=` locator would only prove the substring appears *somewhere* in the one `<pre>`
  // element, not how many times — so count occurrences in the raw text instead.
  await expect.poll(async () => {
    const text = await document.textContent();
    return (text?.match(/"has-orders"/g) ?? []).length;
  }).toBe(1);
});

test('cancelling with Escape does not insert the cancelled buffer', async ({ page }) => {
  await page.goto('/');
  await buildRootExpression(page, 'is-active & is-adult');
  await expect(page.getByLabel('rule document')).toContainText('"and"');

  await page.getByRole('button', { name: 'insert after $.rule.and[0]', exact: true }).click();
  await expect(pendingContent(page)).toBeFocused();
  await page.keyboard.type('has-orders');
  await page.keyboard.press('Escape');
  await page.keyboard.press('Escape');

  await expect(page.locator('.node-row-pending')).toHaveCount(0);
  const document = page.getByLabel('rule document');
  await expect(document).not.toContainText('has-orders');
  await expect(document).toContainText('"is-active"');
  await expect(document).toContainText('"is-adult"');
});
