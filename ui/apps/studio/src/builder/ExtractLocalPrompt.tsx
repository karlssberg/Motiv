import { useEffect, useState, type RefObject } from 'react';
import { finishLocalName, isValidLocalName } from '@motiv-rules/core';
import { useRuleEditorStore } from '@motiv-rules/react';
import { usePopoverCard } from './usePopoverCard.js';
import { LocalNameInput } from './LocalNameInput.js';

/**
 * The "extract this subtree into a reusable definition" prompt: a small card seeded from the
 * node's own name or its summary text, that turns the node at `path` into `{ local: name }` and
 * files the rest away under `definitions[name]`.
 *
 * It has no trigger of its own — unlike `NodeMenu`, which draws the button that opens it, this
 * component is opened from wherever a caller's own control lives (a menu item, in practice), so it
 * takes that control's ref instead and hands it to `usePopoverCard` as the anchor: that is what
 * both places the card against and what Escape returns focus to.
 */
export function ExtractLocalPrompt(props: {
  path: string;
  open: boolean;
  setOpen: (open: boolean) => void;
  triggerRef?: RefObject<HTMLElement> | undefined;
  seed: string;
  onDone?: ((name: string) => void) | undefined;
}) {
  const { path, open, setOpen, triggerRef, seed, onDone } = props;
  const store = useRuleEditorStore();
  // Unlike `NodeMenu`, this card draws no trigger of its own — it is opened from wherever the
  // caller's own control lives — so the caller's ref is handed in as the anchor to measure and
  // restore focus to, instead of `usePopoverCard`'s own (here-unused) `trigger` ref.
  const { card, style, close } = usePopoverCard(open, setOpen, triggerRef);

  const [name, setName] = useState(() => finishLocalName(seed));
  // Re-seeds on every opening rather than only on mount, so a card kept alive by its host (as
  // `NodeMenu`'s is) starts fresh for whichever node it is opened against next, instead of
  // showing the previous node's name.
  useEffect(() => {
    if (open) setName(finishLocalName(seed));
  }, [open, seed]);

  if (!open) return null;

  const taken = new Set(Object.keys(store.getState().document.definitions ?? {}));
  const invalid = taken.has(name) || !isValidLocalName(name);

  const create = (): void => {
    if (invalid) return;
    store.defineLocal(path, name);
    onDone?.(name);
    close();
  };

  return (
    <div
      ref={card}
      className="dsl-popover"
      role="dialog"
      aria-label="Extract to definition"
      style={style}
      tabIndex={-1}
    >
      <p className="dialog-title">Extract to definition</p>
      <label className="field">
        <span>Name</span>
        <LocalNameInput
          value={name}
          onChange={setName}
          onCommit={() => setName((current) => finishLocalName(current))}
          ariaLabel={`local name for ${path}`}
          taken={taken}
        />
      </label>
      <div className="dsl-popover-actions">
        <button type="button" onClick={create} disabled={invalid}>Create</button>
        <button type="button" onClick={close}>Cancel</button>
      </div>
    </div>
  );
}
