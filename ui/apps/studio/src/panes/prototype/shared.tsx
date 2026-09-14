// PROTOTYPE — throwaway. Bits every variant renders the same way.
import type { CSSProperties } from 'react';
import type { EvaluationResult } from '@motiv-rules/core';
import { JustificationTree } from '@motiv-rules/react';
import { Tick, Verdict } from '../Verdict.js';
import { Caret } from '../../shell/icons.js';
import type { Side } from './compare.js';

/** The explanation tree exactly as EvaluatePane draws it. */
export function ExplanationTree(props: { result: EvaluationResult; label: string }) {
  const { result } = props;
  return (
    <JustificationTree explanation={result.explanation} label={props.label}>
      {({ row, toggle, groupId }) => {
        const causes = row.assertions.join(', ');
        return (
          <div className="assertion" style={{ '--depth': row.depth } as CSSProperties}>
            {row.hasChildren ? (
              <button
                type="button"
                aria-expanded={!row.collapsed}
                aria-controls={groupId ?? undefined}
                aria-label={`causes of ${causes}`}
                className="node-chev"
                onClick={() => toggle(row.id)}
              >
                <Caret open={!row.collapsed} size={12} />
              </button>
            ) : <Tick satisfied={result.satisfied} />}
            <span>{causes}</span>
          </div>
        );
      }}
    </JustificationTree>
  );
}

export function SideVerdict(props: { side: Side; label?: string }) {
  const { side } = props;
  if (side.status === 'ready') {
    return (
      <Verdict
        satisfied={side.result.satisfied}
        text={side.result.satisfied ? 'Satisfied' : 'Not satisfied'}
        {...(props.label ? { label: props.label } : {})}
      />
    );
  }
  if (side.status === 'loading') return <span className="pane-hint">running…</span>;
  if (side.status === 'error' || side.status === 'unavailable') {
    return <span className="pane-hint proto-muted">{side.message}</span>;
  }
  return null;
}

export function SideBody(props: { side: Side; label: string }) {
  const { side } = props;
  if (side.status === 'ready') return <ExplanationTree result={side.result} label={props.label} />;
  if (side.status === 'error') return <p role="alert" className="error">{side.message}</p>;
  if (side.status === 'unavailable') return <p className="pane-hint">{side.message}</p>;
  return null;
}
