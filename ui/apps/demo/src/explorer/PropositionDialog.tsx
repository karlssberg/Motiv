import { useState } from 'react';
import type { PropositionListEntry } from '@motiv/rules-core';

/** What the New / Derive / Override flows start from. */
export interface DialogSeed {
  /** Prefilled name — a trailing dot when deriving, so the namespace is kept and the leaf is typed. */
  name: string;
  modelType: string;
  /**
   * The proposition the new document should reference, when the flow already knows which one that
   * is. Set for Derive. Null for New, and null for Override too: an override is authored under the
   * name it overrides, and a reference back to the name being defined would be a cycle onto itself
   * — so those two flows ask, rather than assume. Either way the answer is a source picked from
   * what is already registered: a UI-authored proposition is composition-only, so *every* one of
   * them is a reference to something that exists. Named for the field it seeds rather than for the
   * one flow that arrives with it already answered.
   */
  startsFrom: string | null;
  /** What this flow is called — the heading and the dialog's accessible name. */
  title: string;
}

/**
 * Ties the "nothing to start from" explanation to the Create button it disables. A constant id is
 * safe because the dialog is modal — only ever one of it on the page.
 */
const NO_SOURCE_ID = 'dialog-no-source';

/** The values a create is built from. `startsFrom` is the spec the new document references. */
export interface DialogValues {
  name: string;
  modelType: string;
  description: string | null;
  startsFrom: string;
}

/**
 * One dialog for New, Derive and Override. All three are seeded creates rather than their own
 * concepts, so there is no second persistence shape and no lineage to keep — the reference graph
 * already records exactly what a "derived from" edge would, and layering records the override.
 * What separates them is only how much of the form is already answered.
 */
export function PropositionDialog(props: {
  seed: DialogSeed;
  /** Everything registered. What can be started from is drawn from this, per model type. */
  sources: PropositionListEntry[];
  error: string | null;
  onCancel: () => void;
  onCreate: (values: DialogValues) => void;
}) {
  const [name, setName] = useState(props.seed.name);
  const [modelType, setModelType] = useState(props.seed.modelType);
  const [description, setDescription] = useState('');
  const [startsFrom, setStartsFrom] = useState<string | null>(props.seed.startsFrom);

  const trimmedName = name.trim();
  const trimmedDescription = description.trim();

  // The seed's model type is offered even when nothing is registered under it, so the select can
  // always show what the flow was opened with.
  const modelTypes = [...new Set([
    props.seed.modelType,
    ...props.sources.map((entry) => entry.modelType),
  ])].sort();

  // A source has to be over the same model to bind, and cannot be the name being defined — that
  // would be a reference straight back onto itself. Only the direct case is excluded here; a chosen
  // source that reaches the name being defined further along is the server's to reject, and
  // `describeFailure` surfaces that rejection.
  const sourceNames = props.sources
    .filter((entry) => entry.modelType === modelType && entry.name !== trimmedName)
    .map((entry) => entry.name)
    .sort();

  // Derived rather than corrected in an effect: changing the model type (or typing the name of the
  // very proposition picked) can invalidate the choice, and a selection that no longer appears in
  // the list must not be what a create is built from.
  const picked = startsFrom !== null && sourceNames.includes(startsFrom) ? startsFrom : null;
  const selectedSource = picked ?? sourceNames[0] ?? null;

  // One statement of what makes the form submittable, shared by the Enter key and the button.
  const canCreate = trimmedName !== '' && selectedSource !== null;

  const submit = (): void => {
    // `canCreate` already covers both, and is restated only because the type-checker cannot narrow
    // `selectedSource` through it.
    if (!canCreate || selectedSource === null) return;
    props.onCreate({
      name: trimmedName,
      modelType,
      description: trimmedDescription === '' ? null : trimmedDescription,
      startsFrom: selectedSource,
    });
  };

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
              {modelTypes.map((model) => <option key={model} value={model}>{model}</option>)}
            </select>
          </label>
        </div>

        {/* Left changeable even when the flow seeded it. Derive is a shortcut into ordinary
            authoring, not a separate concept, so the same form answers it — picking the wrong node
            to derive from should be a change of mind, not a cancel and start again. */}
        <div className="dialog-field">
          <label>
            <span>Starts from</span>
            <select
              value={selectedSource ?? ''}
              disabled={sourceNames.length === 0}
              onChange={(event) => setStartsFrom(event.target.value)}
            >
              {sourceNames.map((sourceName) => (
                <option key={sourceName} value={sourceName}>{sourceName}</option>
              ))}
            </select>
          </label>
          {sourceNames.length === 0
            ? (
              // Referenced from Create below: the select is disabled and so out of the tab order,
              // which would leave the reason the button is dead unreachable from the button itself.
              <small className="dialog-warning" id={NO_SOURCE_ID}>
                Nothing to start from: an authored proposition is composed from ones that already
                exist, and no other {modelType} proposition is registered.
              </small>
            )
            : <small>The new proposition begins as a reference to this one; compose it from there.</small>}
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
          <button
            type="button"
            className="btn"
            disabled={!canCreate}
            aria-describedby={sourceNames.length === 0 ? NO_SOURCE_ID : undefined}
            onClick={submit}
          >
            Create
          </button>
        </div>
      </div>
    </div>
  );
}
