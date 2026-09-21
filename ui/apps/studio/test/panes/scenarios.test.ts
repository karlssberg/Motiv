import { describe, it, expect, vi } from 'vitest';
import type { EvaluationResult, RulesApiClient } from '@motiv-rules/core';
import type { ScenarioEntry } from '@motiv-rules/core';
import {
  addScenario, cloneScenario, diffAssertions, editScenario, fromStored, outcomeChanged, removeScenario,
  runScenario, toggleScenario, withSaved, withSaving, withViolations, type Scenario,
} from '../../src/panes/scenarios.js';

/** Four stored rows, as the host lists them — what the browser used to seed for itself. */
const STORED: ScenarioEntry[] = [
  ['s1', 'Active adult, 3 orders', '{ "customerId": "cust-42", "age": 30, "isActive": true, "orderCount": 3, "orders": [{ "total": 120 }] }'],
  ['s2', 'Minor', '{ "customerId": "cust-7", "age": 16, "isActive": true, "orderCount": 1, "orders": [{ "total": 20 }] }'],
  ['s3', 'Dormant account', '{ "customerId": "cust-9", "age": 41, "isActive": false, "orderCount": 12, "orders": [{ "total": 300 }] }'],
  ['s4', 'New, no orders', '{ "customerId": "cust-1", "age": 25, "isActive": true, "orderCount": 0 }'],
].map(([id, name, model]) => ({
  id: id!, name: name!, model: model!, expectedSatisfied: null, sourceDecisionId: null, version: 1, author: 'system', timestampUtc: '2026-09-20T00:00:00Z',
}));
const seedScenarios = (): Scenario[] => fromStored(STORED);

const result = (satisfied: boolean, ...assertions: string[]): EvaluationResult => ({
  satisfied, reason: assertions.join(' & '), assertions, values: assertions,
  justification: assertions.join('\n'), explanation: { assertions, underlying: [] },
});

const client = (live: EvaluationResult, draft: EvaluationResult): RulesApiClient =>
  ({
    evaluateRule: vi.fn().mockResolvedValue(live),
    evaluate: vi.fn().mockResolvedValue(draft),
  }) as unknown as RulesApiClient;

describe('scenarios', () => {
  it('turns stored rows into unevaluated scenarios at their versions, hiding host bookkeeping ids', () => {
    const rows = fromStored([...STORED, { ...STORED[0]!, id: '__seeded', name: 'seeded' }]);
    expect(rows.map((r) => r.id)).toEqual(['s1', 's2', 's3', 's4']);
    for (const row of rows) {
      expect(row.name).not.toBe('');
      expect(row.version).toBe(1);
      expect(row.saving).toBe('idle');
      expect(row.comparison).toEqual({ live: { status: 'idle' }, draft: { status: 'idle' } });
    }
  });

  it('keeps a stored row’s expected verdict and source decision, so a save from Studio does not erase them', () => {
    const [held] = fromStored([{ ...STORED[0]!, expectedSatisfied: true, sourceDecisionId: 'd-1' }]);
    expect(held!.expectedSatisfied).toBe(true);
    expect(held!.sourceDecisionId).toBe('d-1');
    // A fresh row holds neither; a clone keeps the expectation (same input) but is not the decision's.
    const [added] = addScenario([]);
    expect(added!.expectedSatisfied).toBeNull();
    expect(added!.sourceDecisionId).toBeNull();
    const [, copy] = cloneScenario([held!], held!.id);
    expect(copy!.expectedSatisfied).toBe(true);
    expect(copy!.sourceDecisionId).toBeNull();
  });

  it('marks a row saving, then saved at the store’s version, or flagged with why it did not save', () => {
    const rows = seedScenarios();
    const saving = withSaving(rows, 's2', 'saving');
    expect(saving[1]!.saving).toBe('saving');
    const saved = withSaved(saving, 's2', 7);
    expect(saved[1]).toMatchObject({ saving: 'idle', version: 7 });
    const conflicted = withSaving(saved, 's2', 'conflict', 'changed elsewhere');
    expect(conflicted[1]).toMatchObject({ saving: 'conflict', saveError: 'changed elsewhere' });
  });

  it('diffs two assertion lists into removed, same and added', () => {
    expect(diffAssertions(['a', 'b'], ['b', 'c'])).toEqual([
      { kind: 'removed', text: 'a' },
      { kind: 'same', text: 'b' },
      { kind: 'added', text: 'c' },
    ]);
  });

  it('reports a change when the verdicts or the assertions differ, and null before a run', () => {
    const idle: Scenario = seedScenarios()[0]!;
    expect(outcomeChanged(idle.comparison)).toBeNull();
    expect(outcomeChanged({ live: { status: 'ready', result: result(true, 'a') }, draft: { status: 'ready', result: result(true, 'a') } })).toBe(false);
    expect(outcomeChanged({ live: { status: 'ready', result: result(true, 'a') }, draft: { status: 'ready', result: result(false, 'a') } })).toBe(true);
    expect(outcomeChanged({ live: { status: 'ready', result: result(true, 'a') }, draft: { status: 'ready', result: result(true, 'b') } })).toBe(true);
  });

  it('runs a scenario against the live rule by name and the draft document', async () => {
    const api = client(result(true, 'live says yes'), result(false, 'draft says no'));
    const row = seedScenarios()[0]!;

    const comparison = await runScenario(api, {
      ruleName: 'can-checkout', modelType: 'customer', document: { rule: { spec: 'x' } },
    }, row);

    expect(api.evaluateRule).toHaveBeenCalledWith('can-checkout', JSON.parse(row.model));
    expect(api.evaluate).toHaveBeenCalledWith({ modelType: 'customer', document: { rule: { spec: 'x' } }, model: JSON.parse(row.model) });
    expect(comparison.live).toEqual({ status: 'ready', result: result(true, 'live says yes') });
    expect(comparison.draft).toEqual({ status: 'ready', result: result(false, 'draft says no') });
  });

  it('reports each side’s failure on its own, so one broken side does not hide the other', async () => {
    const api = {
      evaluateRule: vi.fn().mockRejectedValue(new Error('down')),
      evaluate: vi.fn().mockResolvedValue(result(true, 'fine')),
    } as unknown as RulesApiClient;
    const comparison = await runScenario(api, { ruleName: 'r', modelType: 'customer', document: { rule: { spec: 'x' } } }, seedScenarios()[0]!);
    expect(comparison.live).toEqual({ status: 'error', message: 'down' });
    expect(comparison.draft.status).toBe('ready');
  });

  it('refuses to run a scenario whose model is not JSON', async () => {
    const api = client(result(true), result(true));
    const row: Scenario = { ...seedScenarios()[0]!, model: '{ not json' };
    const comparison = await runScenario(api, { ruleName: 'r', modelType: 'customer', document: { rule: { spec: 'x' } } }, row);
    expect(comparison.live).toEqual({ status: 'error', message: 'Scenario model is not valid JSON.' });
    expect(comparison.draft).toEqual({ status: 'error', message: 'Scenario model is not valid JSON.' });
    expect(api.evaluate).not.toHaveBeenCalled();
  });

  it('editing the model drops the row back to unevaluated; renaming does not', () => {
    const evaluated: Scenario = {
      ...seedScenarios()[0]!,
      comparison: { live: { status: 'ready', result: result(true) }, draft: { status: 'ready', result: result(true) } },
    };
    const renamed = editScenario([evaluated], evaluated.id, { name: 'Other' })[0]!;
    expect(renamed.comparison.live.status).toBe('ready');
    const changed = editScenario([evaluated], evaluated.id, { model: '{}' })[0]!;
    expect(changed.comparison).toEqual({ live: { status: 'idle' }, draft: { status: 'idle' } });
  });

  it('clones beneath the source, unevaluated and named as a copy; adds at the end; removes by id', () => {
    const rows = seedScenarios();
    const [first, second] = rows;
    const cloned = cloneScenario(rows, first!.id);
    expect(cloned[1]!.name).toBe(`${first!.name} (copy)`);
    expect(cloned[1]!.model).toBe(first!.model);
    expect(cloned[1]!.id).not.toBe(first!.id);
    expect(cloned[2]!.id).toBe(second!.id);

    const added = addScenario(rows);
    expect(added.at(-1)!.name).toBe(`Scenario ${rows.length + 1}`);

    expect(removeScenario(rows, first!.id).map((r) => r.id)).not.toContain(first!.id);
  });

  it('gives every added or cloned row a fresh string id, unsaved until the store answers', () => {
    const rows = seedScenarios();
    const added = addScenario(addScenario(rows));
    expect(new Set(added.map((r) => r.id)).size).toBe(rows.length + 2);
    expect(added.at(-1)).toMatchObject({ version: 0, saving: 'idle' });
    expect(typeof added.at(-1)!.id).toBe('string');
    expect(cloneScenario(rows, 's1')[1]).toMatchObject({ version: 0, saving: 'idle' });
  });

  it('opens a new or cloned row, toggles a row, and a deleted row takes its state with it', () => {
    const rows = seedScenarios();
    expect(rows.every((r) => !r.open)).toBe(true);
    expect(addScenario(rows).at(-1)!.open).toBe(true);
    expect(cloneScenario(rows, rows[0]!.id)[1]!.open).toBe(true);
    const toggled = toggleScenario(rows, rows[1]!.id);
    expect(toggled[1]!.open).toBe(true);
    expect(toggleScenario(toggled, rows[1]!.id)[1]!.open).toBe(false);
    expect(removeScenario(toggled, rows[1]!.id).some((r) => r.open)).toBe(false);
  });

  it('records violations against a row, and editing its model clears them', () => {
    const rows = seedScenarios();
    const violation = { path: '$.age', message: 'expected integer, got string' };
    const flagged = withViolations(rows, rows[0]!.id, [violation]);
    expect(flagged[0]!.violations).toEqual([violation]);
    expect(editScenario(flagged, rows[0]!.id, { name: 'Renamed' })[0]!.violations).toEqual([violation]);
    expect(editScenario(flagged, rows[0]!.id, { model: '{}' })[0]!.violations).toEqual([]);
  });
});
