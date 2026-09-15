import type { EvaluationResult, RuleDocument, RulesApiClient } from '@motiv-rules/core';

/**
 * One side of a scenario's comparison: what the live rule decided, or what the draft would. Each
 * side fails on its own, so a server that cannot run the live rule still shows the draft's answer.
 */
export type Side =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; result: EvaluationResult }
  | { status: 'error'; message: string };

export interface Comparison {
  live: Side;
  draft: Side;
}

/** A named sample model, and the last comparison it ran — or none, once its model has changed. */
export interface Scenario {
  id: number;
  name: string;
  /** The model as typed: JSON text, kept verbatim so an edit in progress survives a re-render. */
  model: string;
  comparison: Comparison;
}

/** What a run needs to know about the rule: its live name and the tab's draft. */
export interface RuleUnderTest {
  ruleName: string;
  modelType: string;
  document: RuleDocument;
}

export const UNEVALUATED: Comparison = { live: { status: 'idle' }, draft: { status: 'idle' } };

const NOT_JSON = 'Scenario model is not valid JSON.';

/** Studio's seed scenarios for its customer model — the samples the old panes hard-coded, named. */
const SEED: readonly { name: string; model: string }[] = [
  { name: 'Active adult, 3 orders', model: '{\n  "customerId": "cust-42",\n  "age": 30,\n  "isActive": true,\n  "orderCount": 3,\n  "orders": [{ "total": 120 }]\n}' },
  { name: 'Minor', model: '{\n  "customerId": "cust-7",\n  "age": 16,\n  "isActive": true,\n  "orderCount": 1,\n  "orders": [{ "total": 20 }]\n}' },
  { name: 'Dormant account', model: '{\n  "customerId": "cust-9",\n  "age": 41,\n  "isActive": false,\n  "orderCount": 12,\n  "orders": [{ "total": 300 }]\n}' },
  { name: 'New, no orders', model: '{\n  "customerId": "cust-1",\n  "age": 25,\n  "isActive": true,\n  "orderCount": 0\n}' },
];

let nextId = 1;
const fresh = (name: string, model: string): Scenario => ({ id: nextId++, name, model, comparison: UNEVALUATED });

export const seedScenarios = (): Scenario[] => SEED.map((s) => fresh(s.name, s.model));

export const addScenario = (rows: Scenario[]): Scenario[] =>
  [...rows, fresh(`Scenario ${rows.length + 1}`, SEED[0]!.model)];

/** A copy directly beneath its source, unevaluated: the same input, awaiting its own run. */
export function cloneScenario(rows: Scenario[], id: number): Scenario[] {
  const index = rows.findIndex((r) => r.id === id);
  if (index < 0) return rows;
  const source = rows[index]!;
  return [...rows.slice(0, index + 1), fresh(`${source.name} (copy)`, source.model), ...rows.slice(index + 1)];
}

export const removeScenario = (rows: Scenario[], id: number): Scenario[] => rows.filter((r) => r.id !== id);

/**
 * Renames or re-models one scenario. A new model drops the row back to unevaluated: the last
 * verdict described the old input, and showing it beside the new one would be a lie.
 */
export const editScenario = (rows: Scenario[], id: number, change: { name?: string; model?: string }): Scenario[] =>
  rows.map((r) => (r.id !== id ? r : {
    ...r,
    ...change,
    comparison: change.model !== undefined && change.model !== r.model ? UNEVALUATED : r.comparison,
  }));

export const withComparison = (rows: Scenario[], id: number, comparison: Comparison): Scenario[] =>
  rows.map((r) => (r.id === id ? { ...r, comparison } : r));

const settle = (promise: Promise<EvaluationResult>): Promise<Side> =>
  promise.then(
    (result): Side => ({ status: 'ready', result }),
    (cause: unknown): Side => ({ status: 'error', message: cause instanceof Error ? cause.message : String(cause) }),
  );

/** Runs one scenario on both sides at once. Never throws: every failure lands in a side. */
export async function runScenario(client: RulesApiClient, rule: RuleUnderTest, scenario: Scenario): Promise<Comparison> {
  let model: unknown;
  try {
    model = JSON.parse(scenario.model);
  } catch {
    return { live: { status: 'error', message: NOT_JSON }, draft: { status: 'error', message: NOT_JSON } };
  }
  const [live, draft] = await Promise.all([
    settle(client.evaluateRule(rule.ruleName, model)),
    settle(client.evaluate({ modelType: rule.modelType, document: rule.document, model })),
  ]);
  return { live, draft };
}

export type DiffLine = { kind: 'removed' | 'same' | 'added'; text: string };

/** An order-insensitive line diff of two assertion lists. */
export function diffAssertions(before: readonly string[], after: readonly string[]): DiffLine[] {
  const b = new Set(before);
  const a = new Set(after);
  return [
    ...before.filter((x) => !a.has(x)).map((text): DiffLine => ({ kind: 'removed', text })),
    ...after.filter((x) => b.has(x)).map((text): DiffLine => ({ kind: 'same', text })),
    ...after.filter((x) => !b.has(x)).map((text): DiffLine => ({ kind: 'added', text })),
  ];
}

/** Whether saving the draft would change this scenario's outcome; null until both sides have run. */
export function outcomeChanged(c: Comparison): boolean | null {
  if (c.live.status !== 'ready' || c.draft.status !== 'ready') return null;
  return c.live.result.satisfied !== c.draft.result.satisfied
    || diffAssertions(c.live.result.assertions, c.draft.result.assertions).some((d) => d.kind !== 'same');
}
