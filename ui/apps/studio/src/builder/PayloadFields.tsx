import { PayloadDisclosure, payloadPlaceholder } from './PayloadDisclosure.js';

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
 * The `whenTrue` / `whenFalse` fields of a rule root or a definition in the builder, behind the
 * shared disclosure. Edits go straight to the store, so "remove" clears it at once.
 *
 * Cells of the `.decoration` grid the caller owns, so they sit beside the Name field exactly as
 * they did when they were always shown.
 */
export function PayloadFields(props: PayloadFieldsProps) {
  const { statement, whenTrue, whenFalse, scope, onChange } = props;
  const hasPayload = whenTrue !== '' || whenFalse !== '';

  return (
    <PayloadDisclosure
      hasPayload={hasPayload}
      onRemove={() => onChange({ whenTrue: undefined, whenFalse: undefined })}
    >
      <label className="field">
        <span>When true</span>
        <input
          aria-label={`whenTrue ${scope}`}
          className="control"
          type="text"
          // Mounted either by the link (nothing here yet: take focus) or with the panel, already
          // holding text (leave focus where the user had it).
          autoFocus={!hasPayload}
          placeholder={payloadPlaceholder(statement, 'true')}
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
          placeholder={payloadPlaceholder(statement, 'false')}
          value={whenFalse}
          onChange={(e) => onChange({ whenFalse: e.target.value || undefined })}
        />
      </label>
    </PayloadDisclosure>
  );
}
