// PROTOTYPE — throwaway. Variant B: two columns under one sample — Live | Draft — each with its
// own verdict and full explanation tree. Answers: "show me both reasonings, side by side."
import { useState } from 'react';
import type { RulesApiClient } from '@motiv-rules/core';
import { useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';
import { IconPlay } from '../../shell/icons.js';
import { idle, outcomeChanged, runBoth, type Comparison } from './compare.js';
import { SideBody, SideVerdict } from './shared.js';
import { SAMPLE } from './samples.js';

export function VariantB(props: { client: RulesApiClient; ruleName: string; version?: number | undefined }) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);
  const [modelText, setModelText] = useState(SAMPLE);
  const [comparison, setComparison] = useState<Comparison>(idle);
  const [parseError, setParseError] = useState<string | null>(null);

  const run = async (): Promise<void> => {
    try { JSON.parse(modelText); } catch { setParseError('Sample model is not valid JSON.'); return; }
    setParseError(null);
    setComparison({ live: { status: 'loading' }, draft: { status: 'loading' } });
    setComparison(await runBoth(props.client, props.ruleName, state.document, modelText));
  };
  const changed = outcomeChanged(comparison);

  return (
    <section aria-label="Evaluate" className="pane">
      <div className="pane-header">
        <h2>Evaluate</h2>
        <span className="pane-hint truncate">one sample, both versions of this rule</span>
      </div>
      <div className="pane-body">
        <label className="field">
          <span>Sample model</span>
          <textarea aria-label="sample model" className="control" value={modelText} onChange={(e) => setModelText(e.target.value)} rows={5} />
        </label>
        <div className="run-row">
          <button type="button" className="btn" onClick={() => void run()}><IconPlay size={14} />Evaluate both</button>
          {changed === true && <span className="proto-banner proto-banner-changed">Saving would change this outcome</span>}
          {changed === false && <span className="proto-banner">Draft agrees with live</span>}
        </div>
        {parseError && <p role="alert" className="error">{parseError}</p>}
        {comparison.draft.status !== 'idle' && (
          <div className="proto-columns">
            <div className="proto-col">
              <h3>Live<span className="pane-badge">{props.version !== undefined ? `v${props.version}` : 'server'}</span></h3>
              <SideVerdict side={comparison.live} />
              <SideBody side={comparison.live} label="why, live" />
            </div>
            <div className="proto-col proto-col-draft">
              <h3>Draft<span className="pane-badge">unsaved</span></h3>
              <SideVerdict side={comparison.draft} />
              <SideBody side={comparison.draft} label="why, draft" />
            </div>
          </div>
        )}
      </div>
    </section>
  );
}
