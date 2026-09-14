import { useState } from 'react';

/** The patch a payload edit produces — `undefined` clears a field. */
export interface PayloadPatch {
  whenTrue?: string | undefined;
  whenFalse?: string | undefined;
}

export interface PayloadFieldsProps {
  /** The proposition's name, whose `== true` / `== false` suffix is what Motiv says by default. */
  statement: string | undefined;
  whenTrue: string;
  whenFalse: string;
  /** Suffix for the inputs' accessible names — `at $.rule` or `of definition x`. */
  scope: string;
  onChange: (patch: PayloadPatch) => void;
}

/**
 * The `whenTrue` / `whenFalse` fields of a rule root or a definition, behind a disclosure.
 *
 * Both payloads are optional, and the document model insists they are supplied together — so
 * they are one decision, not two, and the panel offers them as one: a single link at rest, both
 * fields once taken up, a single link to give them back. Two empty boxes on every root would read
 * as two things left undone.
 *
 * The fields stay open once opened this session, whatever is typed, and are open from the start
 * whenever the node already carries text. "Remove" clears both and closes.
 *
 * The placeholders say what Motiv will say without them — the name with its suffix — so what the
 * user is overriding is on screen as they override it. A root with no name has no default to show
 * (the suffix rule applies to a name), so its placeholder is a prompt rather than a guess.
 *
 * A fragment, not a wrapper: the fields are cells of the `.decoration` grid the caller owns, so
 * they sit beside the Name field exactly as they did when they were always shown.
 */
export function PayloadFields(props: PayloadFieldsProps) {
  const { statement, whenTrue, whenFalse, scope, onChange } = props;
  const hasPayload = whenTrue !== '' || whenFalse !== '';
  const [opened, setOpened] = useState(hasPayload);

  if (!hasPayload && !opened) {
    return (
      <p className="payload-hint">
        <button type="button" className="link" onClick={() => setOpened(true)}>
          Describe what it means for this to be true or false…
        </button>
      </p>
    );
  }

  const name = statement?.trim();
  const placeholder = (outcome: 'true' | 'false') =>
    name ? `${name} == ${outcome}` : `what it means when ${outcome}`;
  const remove = () => {
    onChange({ whenTrue: undefined, whenFalse: undefined });
    setOpened(false);
  };

  return (
    <>
      <label className="field">
        <span>When true</span>
        <input
          aria-label={`whenTrue ${scope}`}
          className="control"
          type="text"
          // Focus only when the link took the fields up — not when the panel opens on existing text.
          autoFocus={!hasPayload}
          placeholder={placeholder('true')}
          value={whenTrue}
          onChange={(e) => onChange({ whenTrue: e.target.value || undefined })}
        />
      </label>
      <label className="field">
        <span>When false</span>
        <input
          aria-label={`whenFalse ${scope}`}
          className="control"
          type="text"
          placeholder={placeholder('false')}
          value={whenFalse}
          onChange={(e) => onChange({ whenFalse: e.target.value || undefined })}
        />
      </label>
      <p className="payload-hint">
        <button type="button" className="link link-quiet" onClick={remove}>
          Remove these descriptions
        </button>
      </p>
    </>
  );
}
