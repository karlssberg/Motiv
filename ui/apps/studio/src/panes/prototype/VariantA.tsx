// PROTOTYPE — throwaway. Variant A: "Compare" is a mode of Evaluate. One pane, one sample, one
// merged assertion list with diff marks. Answers: "what does my draft change, in one glance?"
import { useState } from 'react';
import type { RulesApiClient } from '@motiv-rules/core';
import { useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';
import { IconPlay } from '../../shell/icons.js';
import { Tick, Verdict } from '../Verdict.js';
import type { EvaluationResult } from '@motiv-rules/core';
import { diffAssertions, idle, outcomeChanged, runBoth, runDraft, type Comparison } from './compare.js';
import { ExplanationTree, SideVerdict } from './shared.js';
import { SAMPLE } from './samples.js';

type Mode = 'draft' | 'compare';

export function VariantA(props: { client: RulesApiClient; ruleName: string; version?: number | undefined }) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);
  const [mode, setMode] = useState<Mode>('compare');
  const [modelText, setModelText] = useState(SAMPLE);
  const [comparison, setComparison] = useState<Comparison>(idle);
  const [parseError, setParseError] = useState<string | null>(null);

  const run = async (): Promise<void> => {
    try { JSON.parse(modelText); } catch { setParseError('Sample model is not valid JSON.'); return; }
    setParseError(null);
    setComparison({ live: { status: 'loading' }, draft: { status: 'loading' } });
    if (mode === 'draft') {
      setComparison({ live: { status: 'idle' }, draft: await runDraft(props.client, state.document, JSON.parse(modelText)) });
    } else {
      setComparison(await runBoth(props.client, props.ruleName, state.document, modelText));
    }
  };

  const changed = outcomeChanged(comparison);
  const liveLabel = props.version !== undefined ? `Live v${props.version}` : 'Live';

  return (
    <section aria-label="Evaluate" className="pane">
      <div className="pane-header">
        <h2>Evaluate</h2>
        <div className="proto-seg" role="radiogroup" aria-label="what to evaluate">
          <button type="button" role="radio" aria-checked={mode === 'draft'} onClick={() => setMode('draft')}>Draft</button>
          <button type="button" role="radio" aria-checked={mode === 'compare'} onClick={() => setMode('compare')}>Draft vs live</button>
        </div>
        <span className="pane-hint truncate">
          {mode === 'draft' ? 'draft, against a sample model' : `draft against ${liveLabel.toLowerCase()}, same sample`}
        </span>
      </div>
      <div className="pane-body">
        <label className="field">
          <span>Sample model</span>
          <textarea aria-label="sample model" className="control" value={modelText} onChange={(e) => setModelText(e.target.value)} rows={5} />
        </label>
        <div className="run-row">
          <button type="button" className="btn" onClick={() => void run()}><IconPlay size={14} />Evaluate</button>
          {mode === 'draft' && <SideVerdict side={comparison.draft} label="outcome" />}
          {mode === 'compare' && comparison.live.status === 'ready' && comparison.draft.status === 'ready' && (
            <span className="proto-arrowpair">
              <span className="proto-sidelabel">{liveLabel}</span>
              <Verdict satisfied={comparison.live.result.satisfied} text={comparison.live.result.satisfied ? 'Satisfied' : 'Not satisfied'} />
              <span className="proto-arrow" aria-hidden="true">→</span>
              <span className="proto-sidelabel">Draft</span>
              <Verdict satisfied={comparison.draft.result.satisfied} text={comparison.draft.result.satisfied ? 'Satisfied' : 'Not satisfied'} />
              <span className={changed ? 'pane-badge proto-badge-changed' : 'pane-badge'}>{changed ? 'changed' : 'same'}</span>
            </span>
          )}
          {mode === 'compare' && comparison.live.status !== 'ready' && comparison.live.status !== 'idle' && (
            <SideVerdict side={comparison.live} />
          )}
        </div>
        {parseError && <p role="alert" className="error">{parseError}</p>}

        {mode === 'draft' && comparison.draft.status === 'ready' && (
          <ExplanationTree result={comparison.draft.result} label="why" />
        )}

        {mode === 'compare' && comparison.live.status === 'ready' && comparison.draft.status === 'ready' && (
          <DiffList live={comparison.live.result} draft={comparison.draft.result} />
        )}
        {mode === 'compare' && comparison.live.status === 'unavailable' && comparison.draft.status === 'ready' && (
          <ExplanationTree result={comparison.draft.result} label="why (draft)" />
        )}
      </div>
    </section>
  );
}

function DiffList(props: { live: EvaluationResult; draft: EvaluationResult }) {
  const { live, draft } = props;
  return (
    <ul className="proto-diff" aria-label="assertions, live versus draft">
      {diffAssertions(live.assertions, draft.assertions).map((line) => (
        <li key={line.kind + line.text} className={`proto-diff-${line.kind}`}>
          <span className="proto-diff-mark" aria-hidden="true">
            {line.kind === 'removed' ? '−' : line.kind === 'added' ? '+' : ' '}
          </span>
          <Tick satisfied={line.kind === 'removed' ? live.satisfied : draft.satisfied} />
          <span>{line.text}</span>
          <span className="proto-diff-who">
            {line.kind === 'removed' ? 'live only' : line.kind === 'added' ? 'draft only' : ''}
          </span>
        </li>
      ))}
    </ul>
  );
}
