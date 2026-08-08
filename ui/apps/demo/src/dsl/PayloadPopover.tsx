import { useState, type CSSProperties, type Ref } from 'react';
import {
  getNode,
  type Catalog, type Payload, type RuleEditorStore,
} from '@motiv-rules/core';
import type { DecorationPatch } from '../decorationPatch.js';

/** Metadata types whose payloads are plain text rather than JSON objects. */
const STRING_METADATA_TYPES = new Set(['String', 'Explanation']);

/** The editable draft of one node's decorations. */
interface Draft {
  name: string;
  whenTrue: string;
  whenFalse: string;
}

/** Renders a stored payload for editing: strings verbatim, objects pretty-printed as JSON. */
function draftOf(payload: Payload | undefined): string {
  if (payload === undefined) return '';
  return typeof payload === 'string' ? payload : JSON.stringify(payload, null, 2);
}

/** The outcome of reading one payload field back: a value (or a clear), or a parse failure. */
type FieldResult =
  | { ok: true; value: Payload | undefined }
  | { ok: false; message: string };

/** Reads a draft field back into a payload. An emptied field clears the payload. */
function readPayload(draft: string, objectMode: boolean, label: string): FieldResult {
  const trimmed = draft.trim();
  if (trimmed === '') return { ok: true, value: undefined };
  if (!objectMode) return { ok: true, value: trimmed };
  try {
    return { ok: true, value: JSON.parse(trimmed) as Payload };
  } catch {
    return { ok: false, message: `${label} is not valid JSON.` };
  }
}

/** A labelled multi-line payload field. */
function PayloadField(props: { label: string; value: string; onChange: (next: string) => void }) {
  const { label, value, onChange } = props;

  return (
    <label className="field">
      <span>{label}</span>
      <textarea
        className="control"
        rows={3}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
    </label>
  );
}

/**
 * Edits the `name` and `whenTrue`/`whenFalse` payloads of one spec node. Payloads are plain
 * strings when the catalog says the spec carries string metadata, and JSON objects otherwise —
 * in which case they are validated on save, so a malformed object never reaches the store.
 *
 * Where the card sits is the caller's business: it measures the token the card is anchored to and
 * passes in the resulting offsets (and takes the element ref it measured the card itself against).
 */
export function PayloadPopover(props: {
  store: RuleEditorStore;
  catalog: Catalog;
  path: string;
  spec: string;
  onClose: () => void;
  /** The measured placement, in viewport coordinates. */
  style?: CSSProperties;
  /** Handed the card's root, so the caller can measure it. */
  cardRef?: Ref<HTMLDivElement>;
}) {
  const { store, catalog, path, spec, onClose, style, cardRef } = props;

  const entry = catalog.specs.find((candidate) => candidate.name === spec);
  const objectMode = entry !== undefined && !STRING_METADATA_TYPES.has(entry.metadataType);
  const properties = Object.keys(
    (entry && catalog.metadataTypes?.[entry.metadataType]?.properties) ?? {},
  );

  const [draft, setDraft] = useState<Draft>(() => {
    const node = getNode(store.getState().document, path);
    return {
      name: node?.name ?? '',
      whenTrue: draftOf(node?.whenTrue),
      whenFalse: draftOf(node?.whenFalse),
    };
  });
  const [error, setError] = useState<string | null>(null);

  const patch = (change: Partial<Draft>): void => setDraft((current) => ({ ...current, ...change }));

  const save = (): void => {
    const whenTrue = readPayload(draft.whenTrue, objectMode, 'When true');
    const whenFalse = readPayload(draft.whenFalse, objectMode, 'When false');
    if (!whenTrue.ok || !whenFalse.ok) {
      const failures = [whenTrue, whenFalse].flatMap((field) => (field.ok ? [] : [field.message]));
      setError(failures.join(' '));
      return;
    }

    store.setName(path, draft.name.trim() || undefined);
    store.setDecoration(path, { whenTrue: whenTrue.value, whenFalse: whenFalse.value } as DecorationPatch);
    onClose();
  };

  return (
    <div
      ref={cardRef}
      className="dsl-popover"
      role="dialog"
      aria-label={`Payload for ${spec}`}
      style={style}
      // The card is opened deliberately and dismissed the same way, so it takes focus — and
      // Escape gives it back, from wherever inside the card the caret has got to.
      tabIndex={-1}
      onKeyDown={(event) => {
        if (event.key === 'Escape') onClose();
      }}
    >
      <div className="dsl-popover-head">
        <span className="dsl-popover-spec">{spec}</span>
        {entry?.isAsync && <span className="dsl-badge">async</span>}
        <button type="button" className="dsl-popover-close" aria-label="Close" onClick={onClose}>×</button>
      </div>

      {entry?.description && <p className="dsl-popover-desc">{entry.description}</p>}
      {entry && <p className="dsl-popover-meta">{entry.modelType} → {entry.metadataType}</p>}

      <label className="field">
        <span>Name</span>
        <input
          className="control"
          type="text"
          value={draft.name}
          onChange={(e) => patch({ name: e.target.value })}
        />
      </label>
      <PayloadField label="When true" value={draft.whenTrue} onChange={(whenTrue) => patch({ whenTrue })} />
      <PayloadField label="When false" value={draft.whenFalse} onChange={(whenFalse) => patch({ whenFalse })} />

      {objectMode && (
        <p className="dsl-popover-hint">
          {properties.length > 0
            ? `JSON object · properties: ${properties.join(', ')}`
            : 'JSON object'}
        </p>
      )}
      {error && <p className="dsl-popover-error" role="alert">{error}</p>}

      <div className="dsl-popover-actions">
        <button type="button" onClick={save}>Save</button>
        <button type="button" onClick={onClose}>Cancel</button>
      </div>
    </div>
  );
}
