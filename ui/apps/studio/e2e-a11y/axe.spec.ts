import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import type { Result } from 'axe-core';
// `test` carries the API fixtures (and refuses a call it has none for) — see `stubs.ts`.
import { expect, test } from './stubs.js';
// The one definition of the AA floor. The conformance record's mechanical claims are checked
// against the same constant, so "enforced by axe" in the report is true of the sweep that runs here
// rather than of one someone remembers — see `a11y/criteria.ts`.
import { WCAG_AA } from '../a11y/criteria.js';

/**
 * The mechanical half of WCAG 2.1 AA, on every Studio view (ticket 18).
 *
 * axe catches roughly half of AA — the half that is a fact about the markup. The other half (focus
 * order, announcement quality, whether a generated label means anything) needs a person and a
 * screen reader, and is scripted in `docs/accessibility/index.md` rather than here. Neither
 * substitutes for the other, which is why this file passing is not itself the conformance claim.
 *
 * Two axes of coverage, both of which cost nothing to add and both of which caught real defects:
 *
 * - **State, not just route.** A palette that is never opened, a modal that is never shown and a
 *   popover that is never triggered contribute no nodes to a scan of the page behind them. A suite
 *   that only visited routes would report green over exactly the four surfaces the ticket names as
 *   the hard cases.
 * - **Both colour schemes.** The stylesheet defines a second palette under
 *   `prefers-color-scheme: dark`, and a contrast rule that holds in one says nothing about the
 *   other. Scanning only the default would have left half the palette unchecked.
 */

/**
 * A violation reduced to what a reader of a failed run needs: the rule, its help text, and the
 * elements that broke it. A raw axe result runs to hundreds of lines per violation, most of it the
 * HTML of nodes that are fine, and a diff of that is unreadable in a CI log.
 */
function readable(violations: Result[]): string[] {
  return violations.map((violation) => {
    const targets = violation.nodes.map((node) => node.target.join(' ')).join(', ');
    return `${violation.id} (${violation.impact ?? 'unknown'}): ${violation.help} — ${targets}`;
  });
}

/** Scan whatever is currently on screen, and fail naming the rules that were broken. */
async function scan(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page }).withTags([...WCAG_AA]).analyze();
  expect(readable(results.violations)).toEqual([]);
}

/** Load a route — the API is already answered from fixtures — and wait for the chrome to settle. */
async function visit(page: Page, route: string): Promise<void> {
  await page.goto(route);
  await expect(page.getByRole('link', { name: 'Rules' })).toBeVisible();
}

/**
 * Type a composite into the root row, so the builder under scan has a subtree.
 *
 * Authored rather than loaded: a rule reaches the editor through the rules palette, and driving
 * that to reach the builder would make every builder scan depend on the palette working. Typing
 * is also what a user does, and it leaves the row's inline CodeMirror editor on screen for a
 * moment — which is itself one of the surfaces worth having been through.
 */
async function composeRule(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'edit expression at $.rule' }).click();
  await page.keyboard.press('ControlOrMeta+a');
  await page.keyboard.type('customer.is-active & customer.is-adult');
  await page.keyboard.press('Enter');
  await expect(page.getByRole('combobox', { name: /^operator at \$\.rule/ })).toBeVisible();
}

/** One thing to scan: a name, and whatever it takes to get the page into that state. */
interface Surface {
  name: string;
  reach: (page: Page) => Promise<void>;
}

const VIEWS: readonly Surface[] = [
  {
    name: 'the rules page — builder, evaluate and checkout',
    reach: async (page) => {
      await visit(page, '/#/rules');
      await expect(page.getByRole('group', { name: 'rule composition' })).toBeVisible();
    },
  },
  {
    name: 'the builder, holding a composition',
    reach: async (page) => {
      await visit(page, '/#/rules');
      await composeRule(page);
    },
  },
  {
    name: 'an evaluation, with its justification on screen',
    reach: async (page) => {
      await visit(page, '/#/rules');
      await page.getByRole('button', { name: 'Evaluate' }).click();
      await expect(page.getByRole('group', { name: /^why this rule was/ })).toBeVisible();
    },
  },
  {
    name: 'the DSL surface, whose CodeMirror accessibility is inherited',
    reach: async (page) => {
      await visit(page, '/#/rules');
      await page.getByRole('tab', { name: 'DSL' }).click();
      await expect(page.getByRole('textbox', { name: 'rule DSL' })).toBeVisible();
    },
  },
  {
    name: 'the propositions page',
    reach: async (page) => { await visit(page, '/#/propositions'); },
  },
  {
    name: 'the propositions page, with one selected',
    reach: async (page) => { await visit(page, '/#/propositions/customer.is-verified'); },
  },
  {
    name: 'the admin page, with grants to administer',
    reach: async (page) => {
      await visit(page, '/#/admin');
      await expect(page.getByRole('link', { name: 'Admin' })).toBeVisible();
    },
  },
];

const HARD_SURFACES: readonly Surface[] = [
  {
    name: 'the command palette, browsing its namespace tree',
    reach: async (page) => {
      await visit(page, '/#/propositions');
      await page.getByRole('button', { name: 'Open' }).click();
      await expect(page.getByRole('dialog', { name: 'Propositions' })).toBeVisible();
    },
  },
  {
    name: 'the command palette, filtered to a result list',
    reach: async (page) => {
      await visit(page, '/#/propositions');
      await page.getByRole('button', { name: 'Open' }).click();
      const palette = page.getByRole('dialog', { name: 'Propositions' });
      await palette.getByRole('combobox').fill('customer');
      await expect(palette.getByRole('option').first()).toBeVisible();
    },
  },
  {
    name: 'the command palette, filtered to nothing at all',
    reach: async (page) => {
      await visit(page, '/#/propositions');
      await page.getByRole('button', { name: 'Open' }).click();
      const palette = page.getByRole('dialog', { name: 'Propositions' });
      await palette.getByRole('combobox').fill('no-such-proposition');
      await expect(palette.getByRole('status')).toHaveText(/^0 of/);
    },
  },
  {
    name: 'the modal document viewer',
    reach: async (page) => {
      await visit(page, '/#/rules');
      await page.getByRole('button', { name: 'JSON' }).click();
      await expect(page.getByLabel('rule document')).toBeVisible();
    },
  },
  {
    name: "the builder's operator picker, open over the row it belongs to",
    reach: async (page) => {
      await visit(page, '/#/rules');
      await composeRule(page);
      await page.getByRole('combobox', { name: /^operator at \$\.rule/ }).click();
      await expect(page.getByRole('listbox', { name: /^operators for/ })).toBeVisible();
    },
  },
  {
    name: "the builder's row menu",
    reach: async (page) => {
      await visit(page, '/#/rules');
      await page.getByRole('button', { name: 'actions for $.rule' }).click();
      await expect(page.getByRole('menuitem', { name: 'Details' })).toBeVisible();
    },
  },
  {
    name: "a node's detail panel",
    reach: async (page) => {
      await visit(page, '/#/rules');
      await page.getByRole('button', { name: 'actions for $.rule' }).click();
      await page.getByRole('menuitem', { name: 'Details' }).click();
      await expect(page.getByLabel('name at $.rule')).toBeVisible();
    },
  },
];

for (const scheme of ['light', 'dark'] as const) {
  test.describe(`${scheme} scheme`, () => {
    test.use({ colorScheme: scheme });

    test.describe('every view', () => {
      for (const surface of VIEWS) {
        test(surface.name, async ({ page }) => {
          await surface.reach(page);
          await scan(page);
        });
      }
    });

    test.describe('the hard surfaces, in the state they are hard in', () => {
      for (const surface of HARD_SURFACES) {
        test(surface.name, async ({ page }) => {
          await surface.reach(page);
          await scan(page);
        });
      }
    });
  });
}
