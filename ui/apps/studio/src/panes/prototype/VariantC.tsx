// PROTOTYPE — throwaway. Variant C: a scenario table. Several named sample models as rows,
// Live | Draft as columns, flipped rows highlighted; expand a row for both explanations.
// Answers: "which customers does saving this affect?"
import { useState } from 'react';
import type { RulesApiClient } from '@motiv-rules/core';
import { useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';
import { Caret, IconNew, IconPlay } from '../../shell/icons.js';
import { Tick } from '../Verdict.js';
import { idle, outcomeChanged, runBoth, type Comparison } from './compare.js';
import { SideBody } from './shared.js';
import { SCENARIOS } from './samples.js';

interface Row { id: number; name: string; json: string; comparison: Comparison; open: boolean; editing: boolean }

let nextId = 100;

export function VariantC(props: { client: RulesApiClient; ruleName: string; version?: number | undefined }) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);
  const [rows, setRows] = useState<Row[]>(
    SCENARIOS.map((s, i) => ({ id: i, name: s.name, json: s.json, comparison: idle, open: false, editing: false })),
  );
  const patch = (id: number, change: Partial<Row>): void =>
    setRows((rs) => rs.map((r) => (r.id === id ? { ...r, ...change } : r)));

  const runAll = async (): Promise<void> => {
    setRows((rs) => rs.map((r) => ({ ...r, comparison: { live: { status: 'loading' }, draft: { status: 'loading' } } })));
    await Promise.all(rows.map(async (r) => {
      try {
        patch(r.id, { comparison: await runBoth(props.client, props.ruleName, state.document, r.json) });
      } catch {
        patch(r.id, { comparison: { live: { status: 'error', message: 'not valid JSON' }, draft: { status: 'error', message: 'not valid JSON' } } });
      }
    }));
  };
  const flipped = rows.filter((r) => outcomeChanged(r.comparison) === true).length;
  const ran = rows.some((r) => r.comparison.draft.status !== 'idle');

  return (
    <section aria-label="Evaluate" className="pane">
      <div className="pane-header">
        <h2>Evaluate</h2>
        <span className="pane-badge">scenarios</span>
        <span className="pane-hint truncate">every sample, live and draft</span>
      </div>
      <div className="pane-body">
        <div className="run-row">
          <button type="button" className="btn" onClick={() => void runAll()}><IconPlay size={14} />Run all</button>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => setRows((rs) => [...rs, { id: nextId++, name: `Scenario ${rs.length + 1}`, json: SCENARIOS[0]!.json, comparison: idle, open: true, editing: true }])}
          >
            <IconNew size={14} />Add scenario
          </button>
          {ran && (
            <span className={flipped > 0 ? 'proto-banner proto-banner-changed' : 'proto-banner'}>
              {flipped === 0 ? 'No scenario changes' : `${flipped} of ${rows.length} scenarios change`}
            </span>
          )}
        </div>
        <table className="proto-table">
          <thead>
            <tr>
              <th />
              <th>Scenario</th>
              <th>Live{props.version !== undefined ? ` v${props.version}` : ''}</th>
              <th>Draft</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => {
              const changed = outcomeChanged(r.comparison);
              return (
                <RowView key={r.id} row={r} changed={changed} onPatch={(c) => patch(r.id, c)} />
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function Cell(props: { side: Comparison['live'] }) {
  const s = props.side;
  if (s.status === 'ready') return <><Tick satisfied={s.result.satisfied} />{s.result.satisfied ? 'yes' : 'no'}</>;
  if (s.status === 'loading') return <span className="pane-hint">…</span>;
  if (s.status === 'idle') return <span className="pane-hint">–</span>;
  return <span className="pane-hint" title={s.message}>n/a</span>;
}

function RowView(props: { row: Row; changed: boolean | null; onPatch: (c: Partial<Row>) => void }) {
  const { row, changed, onPatch } = props;
  return (
    <>
      <tr className={changed ? 'proto-row-changed' : undefined}>
        <td>
          <button type="button" className="node-chev" aria-expanded={row.open} aria-label={`details of ${row.name}`} onClick={() => onPatch({ open: !row.open })}>
            <Caret open={row.open} size={12} />
          </button>
        </td>
        <td>
          <button type="button" className="proto-linkish" onClick={() => onPatch({ open: true, editing: true })}>{row.name}</button>
          {changed && <span className="pane-badge proto-badge-changed">flips</span>}
        </td>
        <td><Cell side={row.comparison.live} /></td>
        <td><Cell side={row.comparison.draft} /></td>
      </tr>
      {row.open && (
        <tr className="proto-detail">
          <td />
          <td colSpan={3}>
            {row.editing && (
              <div className="proto-edit">
                <input aria-label="scenario name" className="control" value={row.name} onChange={(e) => onPatch({ name: e.target.value })} />
                <textarea aria-label="scenario model" className="control" rows={5} value={row.json} onChange={(e) => onPatch({ json: e.target.value })} />
              </div>
            )}
            {row.comparison.draft.status !== 'idle' && (
              <div className="proto-columns">
                <div className="proto-col"><h3>Live</h3><SideBody side={row.comparison.live} label={`why, live, ${row.name}`} /></div>
                <div className="proto-col proto-col-draft"><h3>Draft</h3><SideBody side={row.comparison.draft} label={`why, draft, ${row.name}`} /></div>
              </div>
            )}
          </td>
        </tr>
      )}
    </>
  );
}
