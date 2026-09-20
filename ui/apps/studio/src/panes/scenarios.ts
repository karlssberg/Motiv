import type { EvaluationResult, RuleDocument, RulesApiClient, ScenarioEntry, SchemaViolation } from '@motiv-rules/core';

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

/**
 * A named sample model, and the last comparison it ran — or none, once its model has changed.
 * Everything a row shows lives on the row: whether its detail is open, and the schema violations
 * its last run found. Deleting a row therefore takes its state with it, and editing its model
 * clears what no longer describes the text.
 */
export interface Scenario {
  id: string;
  name: string;
  /** The model as typed: JSON text, kept verbatim so an edit in progress survives a re-render. */
  model: string;
  /** Null for a sample to look at; set for a test to hold. Carried, not edited, by this pane. */
  expectedSatisfied: boolean | null;
  /** The logged decision this scenario was saved from, when it was. Carried, not edited, by this pane. */
  sourceDecisionId: string | null;
  /** The store's version, 0 until first saved. */
  version: number;
  /** Whether the store has the row as shown, is being told, or refused the last write. */
  saving: 'idle' | 'saving' | 'conflict' | 'error';
  saveError?: string | undefined;
  /** An edit landed while a write was in flight: the row goes back to the store once that write answers. */
  dirty: boolean;
  comparison: Comparison;
  open: boolean;
  violations: SchemaViolation[];
}

/** What a run needs to know about the rule: its live name and the tab's draft. */
export interface RuleUnderTest {
  ruleName: string;
  modelType: string;
  document: RuleDocument;
}

export const UNEVALUATED: Comparison = { live: { status: 'idle' }, draft: { status: 'idle' } };

const NOT_JSON = 'Scenario model is not valid JSON.';

/** A fresh id for a row created in the browser: the store keys a scenario by rule and id, so the client may mint it. */
const newId = (): string =>
  typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID().replace(/-/g, '')
    : Array.from({ length: 32 }, () => Math.floor(Math.random() * 16).toString(16)).join('');

/**
 * A row at its defaults — unwritten, unevaluated, closed and holding nothing — with whatever the
 * caller knows about it laid over the top.
 */
const fresh = (
  id: string, name: string, model: string,
  known: Partial<Omit<Scenario, 'id' | 'name' | 'model'>> = {},
): Scenario => ({
  id, name, model,
  expectedSatisfied: null, sourceDecisionId: null,
  version: 0, saving: 'idle', dirty: false, comparison: UNEVALUATED, open: false, violations: [],
  ...known,
});

/** Rows from the store, unevaluated. Ids beginning with `__` are host bookkeeping and never shown. */
export const fromStored = (entries: readonly ScenarioEntry[]): Scenario[] =>
  entries.filter((e) => !e.id.startsWith('__'))
    .map((e) => fresh(e.id, e.name, e.model, {
      version: e.version, expectedSatisfied: e.expectedSatisfied, sourceDecisionId: e.sourceDecisionId,
    }));

/** A new scenario, opened straight away: an empty row is nothing to look at until it is edited. */
export const addScenario = (rows: Scenario[]): Scenario[] =>
  [...rows, fresh(newId(), `Scenario ${rows.length + 1}`, '{\n  \n}', { open: true })];

/** A copy directly beneath its source, unevaluated and open: the same input, awaiting its own run. */
export function cloneScenario(rows: Scenario[], id: string): Scenario[] {
  const index = rows.findIndex((r) => r.id === id);
  if (index < 0) return rows;
  const source = rows[index]!;
  // The same input carries the same expectation, but the copy is not the decision's own record.
  const copy = fresh(newId(), `${source.name} (copy)`, source.model,
    { open: true, expectedSatisfied: source.expectedSatisfied });
  return [...rows.slice(0, index + 1), copy, ...rows.slice(index + 1)];
}

/**
 * The store accepted the row: it is now at the version given. A row edited while that write was
 * in flight stays dirty, which is what sends it back to the store at the new version.
 */
export const withSaved = (rows: Scenario[], id: string, version: number): Scenario[] =>
  rows.map((r) => (r.id === id ? { ...r, version, saving: 'idle' as const, saveError: undefined } : r));

/** Where a row stands with the store: being written (which takes the dirty edit with it), or why the last write did not land. */
export const withSaving = (rows: Scenario[], id: string, saving: Scenario['saving'], saveError?: string): Scenario[] =>
  rows.map((r) => (r.id === id ? { ...r, saving, saveError, dirty: saving === 'saving' ? false : r.dirty } : r));

/** An edit that could not be written yet: the row is marked to go back to the store when it can. */
export const withDirty = (rows: Scenario[], id: string): Scenario[] =>
  rows.map((r) => (r.id === id ? { ...r, dirty: true } : r));

/** Rows the store should hear about now: minted in the browser and never written, or edited while a write was in flight. */
export const pendingSave = (rows: Scenario[]): Scenario[] =>
  rows.filter((r) => r.saving === 'idle' && (r.version === 0 || r.dirty));

export const toggleScenario = (rows: Scenario[], id: string): Scenario[] =>
  rows.map((r) => (r.id === id ? { ...r, open: !r.open } : r));

/** The violations a run found for one row; the row is unevaluated, since it did not run. */
export const withViolations = (rows: Scenario[], id: string, violations: SchemaViolation[]): Scenario[] =>
  rows.map((r) => (r.id === id ? { ...r, violations, comparison: UNEVALUATED } : r));

export const removeScenario = (rows: Scenario[], id: string): Scenario[] => rows.filter((r) => r.id !== id);

/**
 * Renames or re-models one scenario. A new model drops the row back to unevaluated and clears its
 * violations: both described the old input, and showing either beside the new one would be a lie.
 */
export const editScenario = (rows: Scenario[], id: string, change: { name?: string; model?: string }): Scenario[] =>
  rows.map((r) => {
    if (r.id !== id) return r;
    const remodelled = change.model !== undefined && change.model !== r.model;
    return {
      ...r,
      ...change,
      comparison: remodelled ? UNEVALUATED : r.comparison,
      violations: remodelled ? [] : r.violations,
    };
  });

export const withComparison = (rows: Scenario[], id: string, comparison: Comparison): Scenario[] =>
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
