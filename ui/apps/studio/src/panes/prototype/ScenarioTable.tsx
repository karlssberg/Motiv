// PROTOTYPE — throwaway. Round two of variant C: a scenario table with row actions (edit, clone,
// delete, reset). Parameterised on ONE open question — what the row's disclosure reveals before
// the row has been evaluated:
//   'evaluated-only'  no chevron until a run; the name opens the editor; after a run the chevron
//                     reveals the explanations and Edit shows the editor above them.
//   'stacked'         chevron always; the detail is the model editor on top, explanations beneath.
//   'tabs'            chevron always; the detail is tabbed Model | Why, Why disabled until a run.
import { useState, type ReactNode } from 'react';
import type { RulesApiClient } from '@motiv-rules/core';
import { useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';
import { Caret, IconDelete, IconNew, IconPlay, IconRefresh } from '../../shell/icons.js';
import { Tick } from '../Verdict.js';
import { idle, outcomeChanged, runBoth, type Comparison } from './compare.js';
import { SideBody } from './shared.js';
import { SCENARIOS } from './samples.js';

export type Reveal = 'evaluated-only' | 'stacked' | 'tabs';

interface Row { id: number; name: string; json: string; comparison: Comparison; open: boolean; editing: boolean; tab: 'model' | 'why' }

let nextId = 100;
const seed = (): Row[] =>
  SCENARIOS.map((s) => ({ id: nextId++, name: s.name, json: s.json, comparison: idle, open: false, editing: false, tab: 'model' }));

export function ScenarioTable(props: { client: RulesApiClient; ruleName: string; version?: number | undefined; reveal: Reveal }) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);
  const [rows, setRows] = useState<Row[]>(seed);
  const patch = (id: number, change: Partial<Row>): void =>
    setRows((rs) => rs.map((r) => (r.id === id ? { ...r, ...change } : r)));

  const runAll = async (): Promise<void> => {
    setRows((rs) => rs.map((r) => ({ ...r, comparison: { live: { status: 'loading' }, draft: { status: 'loading' } }, tab: 'why' })));
    await Promise.all(rows.map(async (r) => {
      try {
        patch(r.id, { comparison: await runBoth(props.client, props.ruleName, state.document, r.json) });
      } catch {
        patch(r.id, { comparison: { live: { status: 'error', message: 'not valid JSON' }, draft: { status: 'error', message: 'not valid JSON' } } });
      }
    }));
  };
  const clone = (r: Row): void =>
    setRows((rs) => {
      const i = rs.findIndex((x) => x.id === r.id);
      const copy: Row = { ...r, id: nextId++, name: `${r.name} (copy)`, comparison: idle, open: true, editing: true, tab: 'model' };
      return [...rs.slice(0, i + 1), copy, ...rs.slice(i + 1)];
    });
  const add = (): void =>
    setRows((rs) => [...rs, { id: nextId++, name: `Scenario ${rs.length + 1}`, json: SCENARIOS[0]!.json, comparison: idle, open: true, editing: true, tab: 'model' }]);
  const remove = (id: number): void => setRows((rs) => rs.filter((r) => r.id !== id));

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
          <button type="button" className="btn btn-secondary" onClick={add}><IconNew size={14} />Add</button>
          <button type="button" className="btn btn-secondary" onClick={() => setRows(seed())} title="Restore the default scenarios">
            <IconRefresh size={14} />Reset
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
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <RowView
                key={r.id}
                row={r}
                reveal={props.reveal}
                changed={outcomeChanged(r.comparison)}
                onPatch={(c) => patch(r.id, c)}
                onClone={() => clone(r)}
                onDelete={() => remove(r.id)}
              />
            ))}
          </tbody>
        </table>
        {rows.length === 0 && <p className="pane-hint">No scenarios. Add one, or reset to the defaults.</p>}
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

function RowView(props: {
  row: Row; reveal: Reveal; changed: boolean | null;
  onPatch: (c: Partial<Row>) => void; onClone: () => void; onDelete: () => void;
}) {
  const { row, reveal, changed, onPatch } = props;
  const evaluated = row.comparison.draft.status !== 'idle';
  const canDisclose = reveal !== 'evaluated-only' || evaluated;

  // Editing the input invalidates the last result: the verdict no longer describes this model.
  const edit = (change: Partial<Row>): void => onPatch({ ...change, comparison: idle });

  const editor = (
    <div className="proto-edit">
      <input aria-label="scenario name" className="control" value={row.name} onChange={(e) => onPatch({ name: e.target.value })} />
      <textarea aria-label="scenario model" className="control" rows={5} value={row.json} onChange={(e) => edit({ json: e.target.value })} />
      {reveal === 'evaluated-only' && (
        <div className="run-row">
          <button type="button" className="btn btn-secondary" onClick={() => onPatch({ editing: false, open: evaluated })}>Done</button>
        </div>
      )}
    </div>
  );
  const why = evaluated && (
    <div className="proto-columns">
      <div className="proto-col"><h3>Live</h3><SideBody side={row.comparison.live} label={`why, live, ${row.name}`} /></div>
      <div className="proto-col proto-col-draft"><h3>Draft</h3><SideBody side={row.comparison.draft} label={`why, draft, ${row.name}`} /></div>
    </div>
  );

  let detail: ReactNode = null;
  if (reveal === 'evaluated-only') detail = <>{row.editing && editor}{why}</>;
  if (reveal === 'stacked') detail = <>{editor}{why}</>;
  if (reveal === 'tabs') {
    detail = (
      <>
        <div className="proto-seg" role="tablist" aria-label="scenario detail">
          <button type="button" role="tab" aria-selected={row.tab === 'model'} onClick={() => onPatch({ tab: 'model' })}>Model</button>
          <button type="button" role="tab" aria-selected={row.tab === 'why'} disabled={!evaluated} onClick={() => onPatch({ tab: 'why' })}>
            Why{!evaluated && <span className="proto-muted"> (run first)</span>}
          </button>
        </div>
        {row.tab === 'model' ? editor : why}
      </>
    );
  }

  return (
    <>
      <tr className={changed ? 'proto-row-changed' : undefined}>
        <td>
          {canDisclose ? (
            <button type="button" className="node-chev" aria-expanded={row.open} aria-label={`details of ${row.name}`} onClick={() => onPatch({ open: !row.open })}>
              <Caret open={row.open} size={12} />
            </button>
          ) : <span className="proto-chev-gap" aria-hidden="true" />}
        </td>
        <td>
          {reveal === 'evaluated-only' ? (
            <button type="button" className="proto-linkish" onClick={() => onPatch({ open: true, editing: true })}>{row.name}</button>
          ) : (
            <span className="proto-name">{row.name}</span>
          )}
          {changed && <span className="pane-badge proto-badge-changed">flips</span>}
        </td>
        <td><Cell side={row.comparison.live} /></td>
        <td><Cell side={row.comparison.draft} /></td>
        <td className="proto-actions">
          {reveal === 'evaluated-only' && (
            <button type="button" className="proto-action" onClick={() => onPatch({ open: true, editing: true })}>Edit</button>
          )}
          <button type="button" className="proto-action" onClick={props.onClone}>Clone</button>
          <button type="button" className="proto-action proto-action-danger" aria-label={`delete ${row.name}`} onClick={props.onDelete}><IconDelete size={12} /></button>
        </td>
      </tr>
      {row.open && (canDisclose || row.editing) && (
        <tr className="proto-detail">
          <td />
          <td colSpan={4}>{detail}</td>
        </tr>
      )}
    </>
  );
}
