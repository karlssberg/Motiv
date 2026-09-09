import { expect, test as base, type Page, type Route } from '@playwright/test';

/**
 * The API Studio is scanned against.
 *
 * The accessibility sweep serves the built SPA and answers its API itself, rather than driving the
 * .NET host the way `e2e/` does. Two reasons, both about what an audit is for:
 *
 * - **It is a gate, so it has to be deterministic.** Every finding has to be a fact about the
 *   markup, and a view whose contents depend on whatever a live store happens to hold is not that.
 *   Fixed data means a violation that appears is a violation someone introduced.
 * - **It has to run on every pull request.** The host needs the .NET SDK; a static build and a
 *   browser need neither, so the sweep joins the `ui` workflow that already runs on every push
 *   instead of waiting on a second toolchain.
 *
 * What that costs is honest to state: these are the *shapes* the endpoints return, not the
 * endpoints themselves, so a server that started answering differently would not be caught here.
 * The `e2e/` suite drives the real host and is where that belongs.
 */

const CATALOG = {
  specs: [
    {
      name: 'customer.is-active', modelType: 'customer', metadataType: 'String',
      isAsync: false, description: 'The customer account is open.', origin: 'Compiled', parameters: null,
    },
    {
      name: 'customer.is-adult', modelType: 'customer', metadataType: 'String',
      isAsync: false, description: 'The customer is over eighteen.', origin: 'Compiled', parameters: null,
    },
    {
      name: 'customer.is-verified', modelType: 'customer', metadataType: 'String',
      isAsync: false, description: null, origin: 'Authored', parameters: null,
    },
    {
      name: 'orders.is-large', modelType: 'order', metadataType: 'String',
      isAsync: false, description: null, origin: 'Compiled', parameters: null,
    },
  ],
  collections: [{ path: 'orders', parentModelType: 'customer', elementModelType: 'order' }],
  metadataTypes: { String: { type: 'string' } },
  modelTypes: {
    customer: {
      type: ['object', 'null'],
      properties: {
        age: { type: 'integer' },
        isActive: { type: 'boolean' },
        orderCount: { type: 'integer' },
      },
    },
  },
};

const RULES = [
  {
    name: 'checkout.eligibility', modelType: 'customer', metadataType: 'String',
    isAsync: false, isPolicy: true, version: 3, description: 'Whether a customer may check out.',
  },
  {
    name: 'checkout.screening', modelType: 'customer', metadataType: 'String',
    isAsync: true, isPolicy: true, version: 1, description: null,
  },
];

const PROPOSITIONS = CATALOG.specs.map((spec) => ({
  name: spec.name,
  modelType: spec.modelType,
  metadataType: spec.metadataType,
  isAsync: spec.isAsync,
  origin: spec.origin,
  version: spec.origin === 'Compiled' ? 0 : 2,
  description: spec.description ?? null,
  quarantine: [],
}));

/** A composite rule, so the builder under scan has a subtree, an operator badge and nested groups. */
const RULE_DOCUMENT = {
  rule: { and: [{ spec: 'customer.is-active' }, { spec: 'customer.is-adult' }] },
};

const EVALUATION = {
  satisfied: true,
  reason: '(customer.is-active == true) & (customer.is-adult == true)',
  assertions: ['customer is active', 'customer is an adult'],
  values: ['customer is active', 'customer is an adult'],
  justification: 'AND\n    customer is active\n    customer is an adult',
  explanation: {
    assertions: ['customer is active & customer is an adult'],
    underlying: [
      { assertions: ['customer is active'], underlying: [] },
      { assertions: ['customer is an adult'], underlying: [] },
    ],
  },
};

const CHECKOUT = {
  approved: true,
  eligibility: { satisfied: true, reason: 'customer is active', assertions: ['customer is active'] },
  screening: { satisfied: true, reason: 'not on any list', assertions: ['not on any list'] },
  loyalty: null,
};

/** Every route, matched longest-first so `/rules/{name}` is not swallowed by `/rules`. */
const ROUTES: ReadonlyArray<readonly [RegExp, unknown]> = [
  [/\/api\/rules\/catalog$/, CATALOG],
  [/\/api\/rules\/validate$/, { errors: [] }],
  [/\/api\/rules\/evaluate$/, EVALUATION],
  [/\/api\/rules\/propositions\/[^/]+\/dependents$/, []],
  [/\/api\/rules\/propositions\/[^/]+$/, { document: RULE_DOCUMENT, version: 2, origin: 'Authored', hasCompiledDefault: false }],
  [/\/api\/rules\/propositions$/, PROPOSITIONS],
  [/\/api\/rules\/rules\/[^/?]+/, { document: RULE_DOCUMENT, version: 3 }],
  [/\/api\/rules\/rules$/, RULES],
  [/\/api\/admin\/capabilities$/, { grantAdministration: true, administrator: true, devIdentity: true }],
  [/\/api\/admin\/grants$/, [
    { subject: 'analyst@example.test', prefix: 'pricing.', verb: 'author' },
    { subject: 'analyst@example.test', prefix: 'pricing.eu.', verb: 'publish' },
    { subject: 'ops@example.test', prefix: '', verb: 'read' },
  ]],
  [/\/api\/checkout$/, CHECKOUT],
  [/\/api\/decisions$/, []],
];

/**
 * `test`, with the API answered from fixtures for the whole of every test.
 *
 * An automatic fixture rather than a call each spec remembers to make, and one that *checks itself*
 * on the way out: a call with no fixture is recorded and fails the test at teardown. Left to abort
 * quietly it would reach the UI as an error banner — a different page from the one under audit,
 * which axe would then scan and pass, reporting green over a view that never rendered.
 */
export const test = base.extend<{ stubbedApi: void }>({
  stubbedApi: [
    async ({ page }, use) => {
      const unstubbed: string[] = [];

      await page.route('**/api/**', async (route: Route) => {
        const path = new URL(route.request().url()).pathname;
        const match = ROUTES.find(([pattern]) => pattern.test(path));
        if (!match) {
          unstubbed.push(`${route.request().method()} ${path}`);
          await route.abort('failed');
          return;
        }
        await route.fulfill({ json: match[1] as object });
      });

      await use();

      expect(unstubbed, 'every API call the audit makes needs a fixture').toEqual([]);
    },
    { auto: true },
  ],
});

/*
 * The states the coloured surfaces of `--danger` are only drawn in (ticket 168).
 *
 * Each is a route registered *after* the fixture's catch-all, which is how Playwright resolves a
 * pair of matching handlers — most recent first. They live here rather than in the spec because
 * they are the same kind of thing the constants above are: the shape one endpoint returns, in one
 * state. A spec that inlined them would be asserting on a payload it also authored two screens
 * away from the fixture that says what the endpoint normally says.
 *
 * They exist because a translucent tint has no colour of its own — `color-mix(… 12%, transparent)`
 * takes its lightness from whatever is painted beneath it, so the same `--danger` on a pane, a
 * modal and an inset are three different ratios. Only a scan of the composited pixels can tell
 * which of them clears the floor, and a state no view produces contributes no pixels at all.
 */

/** Answer document validation with an error, so the editor's `.error` surfaces render. */
export async function withValidationError(page: Page): Promise<void> {
  await page.route('**/api/rules/validate', (route) => route.fulfill({
    json: {
      errors: [{
        path: '$.rule',
        code: 'UnknownSpec',
        message: 'No proposition named customer.is-active is registered.',
      }],
    },
  }));
}

/** Answer the propositions listing with one proposition quarantined. */
export async function withQuarantinedProposition(page: Page, name: string): Promise<void> {
  await page.route('**/api/rules/propositions', (route) => route.fulfill({
    json: PROPOSITIONS.map((proposition) => (proposition.name === name
      ? {
        ...proposition,
        quarantine: [{ path: '$.rule', code: 'UnknownSpec', message: 'Its definition no longer resolves.' }],
      }
      : proposition)),
  }));
}

/**
 * Answer the catalog with one spec carrying object metadata.
 *
 * The payload popover only *has* a rejection to render when the spec's metadata is an object —
 * a string payload is taken verbatim and cannot fail to parse. The fixture catalog is all
 * `String`, so the popover's error is unreachable without moving one spec off it.
 */
export async function withObjectMetadata(page: Page, name: string): Promise<void> {
  await page.route('**/api/rules/catalog', (route) => route.fulfill({
    json: {
      ...CATALOG,
      specs: CATALOG.specs.map((spec) => (spec.name === name ? { ...spec, metadataType: 'Message' } : spec)),
      metadataTypes: {
        ...CATALOG.metadataTypes,
        Message: { type: 'object', properties: { english: { type: 'string' } } },
      },
    },
  }));
}

export { expect } from '@playwright/test';
