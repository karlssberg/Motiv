/*
  PROTOTYPE — throwaway. Do not promote as-is.

  Question: how should a rule root / definition disclose its optional whenTrue / whenFalse payloads?
  Today both boxes are always shown, empty. Three variants render in the existing node detail panel
  (DecorationEditor and DefinitionDecorationEditor both mount this), switchable via `?variant=A|B|C`:

    A — One link, both boxes. At rest a single link invites a description; clicking shows both
        boxes and a second link that removes them (clears both values). The spec as written.
    B — Per-outcome slots. `true` and `false` are always listed as two rows; each row is a link
        until clicked, then a box with its own × — the two payloads are independently optional.
    C — Default preview, override in place. At rest the panel *shows what Motiv will say* —
        `<name> == true` / `<name> == false` — and the link offers to override it; the boxes
        replace the preview lines in place, the default text becoming their placeholder.

  Shared state rule: the boxes are shown whenever either payload is non-empty, or the user opened
  them this session. "Remove" clears the values and closes. That is the whole state model.
*/
import { useState } from 'react';
import { usePrototypeVariant } from '../prototype/variant.js';
import '../prototype/prototype.css';

export interface PayloadPatch {
  whenTrue?: string | undefined;
  whenFalse?: string | undefined;
}

export interface PayloadFieldsProps {
  /** The name whose `== true` / `== false` suffix is Motiv's fallback explanation. */
  statement: string | undefined;
  whenTrue: string;
  whenFalse: string;
  /** Suffix for the inputs' accessible names — `at $` or `of definition x`. */
  scope: string;
  onChange: (patch: PayloadPatch) => void;
}

const CLEAR: PayloadPatch = { whenTrue: undefined, whenFalse: undefined };

/** The entry point both decoration editors mount. */
export function PayloadDisclosurePrototype(props: PayloadFieldsProps) {
  const variant = usePrototypeVariant();
  if (variant === 'B') return <VariantB {...props} />;
  if (variant === 'C') return <VariantC {...props} />;
  return <VariantA {...props} />;
}

function Link(props: { quiet?: boolean; onClick: () => void; children: string }) {
  return (
    <button
      type="button"
      className={props.quiet ? 'proto-link proto-link-quiet' : 'proto-link'}
      onClick={props.onClick}
    >
      {props.children}
    </button>
  );
}

/* ───────────────────────── A — one link, both boxes ───────────────────────── */

export function VariantA(p: PayloadFieldsProps) {
  const has = p.whenTrue !== '' || p.whenFalse !== '';
  const [opened, setOpened] = useState(has);
  const shown = has || opened;

  if (!shown) {
    return (
      <p className="proto-hint proto-span">
        <Link onClick={() => setOpened(true)}>
          Describe what it means for this to be true or false…
        </Link>
      </p>
    );
  }

  return (
    <div className="proto-a proto-span">
      <div className="proto-a-fields">
        <label className="field">
          <span>When true</span>
          <input
            aria-label={`whenTrue ${p.scope}`}
            className="control"
            type="text"
            autoFocus={!has}
            value={p.whenTrue}
            onChange={(e) => p.onChange({ whenTrue: e.target.value || undefined })}
          />
        </label>
        <label className="field">
          <span>When false</span>
          <input
            aria-label={`whenFalse ${p.scope}`}
            className="control"
            type="text"
            value={p.whenFalse}
            onChange={(e) => p.onChange({ whenFalse: e.target.value || undefined })}
          />
        </label>
      </div>
      <p className="proto-hint">
        <Link quiet onClick={() => { p.onChange(CLEAR); setOpened(false); }}>
          Remove these descriptions
        </Link>
      </p>
    </div>
  );
}

/* ───────────────────────── B — per-outcome slots ───────────────────────── */

function OutcomeSlot(props: {
  outcome: 'true' | 'false';
  value: string;
  scope: string;
  onChange: (value: string | undefined) => void;
}) {
  const { outcome, value } = props;
  const has = value !== '';
  const [opened, setOpened] = useState(has);
  const shown = has || opened;

  return (
    <div className="proto-b-row">
      <span className={`proto-outcome proto-outcome-${outcome}`}>{outcome}</span>
      {shown ? (
        <>
          <input
            aria-label={`when${outcome === 'true' ? 'True' : 'False'} ${props.scope}`}
            className="control"
            type="text"
            autoFocus={!has}
            placeholder={`what it means when ${outcome}`}
            value={value}
            onChange={(e) => props.onChange(e.target.value || undefined)}
          />
          <button
            type="button"
            className="ghost"
            aria-label={`remove when ${outcome} description`}
            title="Remove"
            onClick={() => { props.onChange(undefined); setOpened(false); }}
          >
            ×
          </button>
        </>
      ) : (
        <span className="proto-b-empty">
          <Link onClick={() => setOpened(true)}>add a description</Link>
        </span>
      )}
    </div>
  );
}

export function VariantB(p: PayloadFieldsProps) {
  return (
    <div className="proto-b proto-span">
      <OutcomeSlot outcome="true" value={p.whenTrue} scope={p.scope} onChange={(v) => p.onChange({ whenTrue: v })} />
      <OutcomeSlot outcome="false" value={p.whenFalse} scope={p.scope} onChange={(v) => p.onChange({ whenFalse: v })} />
    </div>
  );
}

/* ───────────────────────── C — default preview, override in place ───────────────────────── */

export function VariantC(p: PayloadFieldsProps) {
  const has = p.whenTrue !== '' || p.whenFalse !== '';
  const [opened, setOpened] = useState(has);
  const shown = has || opened;
  const name = p.statement?.trim() || '(name)';
  const defaults = { whenTrue: `${name} == true`, whenFalse: `${name} == false` };

  return (
    <div className="proto-c proto-span">
      <div className="proto-c-head">Explained as</div>
      <div className="proto-c-grid">
        <span className="proto-c-label">when true</span>
        {shown ? (
          <input
            aria-label={`whenTrue ${p.scope}`}
            className="control"
            type="text"
            autoFocus={!has}
            placeholder={defaults.whenTrue}
            value={p.whenTrue}
            onChange={(e) => p.onChange({ whenTrue: e.target.value || undefined })}
          />
        ) : (
          <code className="proto-c-default">{defaults.whenTrue}</code>
        )}
        <span className="proto-c-label">when false</span>
        {shown ? (
          <input
            aria-label={`whenFalse ${p.scope}`}
            className="control"
            type="text"
            placeholder={defaults.whenFalse}
            value={p.whenFalse}
            onChange={(e) => p.onChange({ whenFalse: e.target.value || undefined })}
          />
        ) : (
          <code className="proto-c-default">{defaults.whenFalse}</code>
        )}
      </div>
      <p className="proto-hint">
        {shown ? (
          <Link quiet onClick={() => { p.onChange(CLEAR); setOpened(false); }}>
            Use the defaults instead
          </Link>
        ) : (
          <Link onClick={() => setOpened(true)}>Write your own explanations…</Link>
        )}
      </p>
    </div>
  );
}
