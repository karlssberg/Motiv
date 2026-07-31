import { useState } from 'react';

/** What the New / Derive / Override flows start from. */
export interface DialogSeed {
  /** Prefilled name — a trailing dot when deriving, so the namespace is kept and the leaf is typed. */
  name: string;
  modelType: string;
  /**
   * The proposition being derived from, whose reference seeds the new document. Null for New, and
   * null for Override too: an override is authored under the name it overrides, and a reference
   * back to the name being defined would be a cycle onto itself.
   */
  deriveFrom: string | null;
  /** What this flow is called — the heading and the dialog's accessible name. */
  title: string;
}

/**
 * One dialog for New, Derive and Override. All three are seeded creates rather than their own
 * concepts, so there is no second persistence shape and no lineage to keep — the reference graph
 * already records exactly what a "derived from" edge would, and layering records the override.
 */
export function PropositionDialog(props: {
  seed: DialogSeed;
  modelTypes: string[];
  error: string | null;
  onCancel: () => void;
  onCreate: (values: { name: string; modelType: string; description: string | null }) => void;
}) {
  const [name, setName] = useState(props.seed.name);
  const [modelType, setModelType] = useState(props.seed.modelType);
  const [description, setDescription] = useState('');

  const trimmedName = name.trim();
  const trimmedDescription = description.trim();
  // One statement of what makes the form submittable, shared by the Enter key and the button.
  const canCreate = trimmedName !== '';

  const submit = (): void => props.onCreate({
    name: trimmedName,
    modelType,
    description: trimmedDescription === '' ? null : trimmedDescription,
  });

  return (
    <div className="dialog-backdrop" role="presentation">
      <div className="dialog" role="dialog" aria-modal="true" aria-label={props.seed.title}>
        <h2 className="dialog-title">{props.seed.title}</h2>

        {/* The hint sits outside the <label>: a label's whole text content becomes its control's
            accessible name, so a sentence of guidance inside it is read out as part of the field's
            name every time the field is reached. */}
        <div className="dialog-field">
          <label>
            <span>Name</span>
            <input
              type="text"
              value={name}
              placeholder="customer.eligibility.is-eligible"
              onChange={(event) => setName(event.target.value)}
              onKeyDown={(event) => { if (event.key === 'Enter' && canCreate) submit(); }}
            />
          </label>
          <small>Dots namespace the proposition; each segment starts with a letter.</small>
        </div>

        <div className="dialog-field">
          <label>
            <span>Model type</span>
            <select value={modelType} onChange={(event) => setModelType(event.target.value)}>
              {props.modelTypes.map((model) => <option key={model} value={model}>{model}</option>)}
            </select>
          </label>
        </div>

        <div className="dialog-field">
          <label>
            <span>Description</span>
            <input type="text" value={description} onChange={(event) => setDescription(event.target.value)} />
          </label>
        </div>

        {props.error !== null && <p className="dialog-error" role="alert">{props.error}</p>}

        <div className="dialog-actions">
          <button type="button" className="btn" onClick={props.onCancel}>Cancel</button>
          <button type="button" className="btn" disabled={!canCreate} onClick={submit}>
            Create
          </button>
        </div>
      </div>
    </div>
  );
}
