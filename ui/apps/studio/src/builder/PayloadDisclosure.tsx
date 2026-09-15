import { useState, type ReactNode } from 'react';
import { Tooltip } from '../shell/Tooltip.js';

/**
 * The disclosure around a `whenTrue` / `whenFalse` pair, wherever it is edited.
 *
 * Both payloads are optional, and the document model insists they are supplied together — so
 * they are one decision, not two, and every surface offers them as one: a single link at rest,
 * the fields once taken up, a single link to give them back. Two empty boxes on every root would
 * read as two things left undone.
 *
 * The fields stay open once opened, whatever is typed, and are open from the start whenever there
 * is already something in them. "Remove" clears through `onRemove` and closes.
 *
 * Renders fragments, not wrappers, so the caller's layout — a grid, a card — owns the cells.
 */
export function PayloadDisclosure(props: {
  hasPayload: boolean;
  onRemove: () => void;
  children: ReactNode;
}) {
  const { hasPayload, onRemove, children } = props;
  const [opened, setOpened] = useState(hasPayload);

  if (!hasPayload && !opened) {
    return (
      <p className="payload-hint">
        <Tooltip text="Add whenTrue and whenFalse text to this node"><button type="button" className="link" onClick={() => setOpened(true)}>
          Describe what it means for this to be true or false…
        </button></Tooltip>
      </p>
    );
  }

  const remove = () => {
    onRemove();
    setOpened(false);
  };

  return (
    <>
      {children}
      <p className="payload-hint">
        <Tooltip text="Clear whenTrue and whenFalse from this node"><button type="button" className="link link-quiet" onClick={remove}>
          Remove these descriptions
        </button></Tooltip>
      </p>
    </>
  );
}

/**
 * What Motiv will say for the outcome without a payload — the name with its `== true` /
 * `== false` suffix — so what a user is overriding is on screen as they override it. With no name
 * there is no default to show (the suffix rule applies to a name), so the placeholder is a prompt
 * rather than a guess.
 */
export function payloadPlaceholder(name: string | undefined, outcome: 'true' | 'false'): string {
  const trimmed = name?.trim();
  return trimmed ? `${trimmed} == ${outcome}` : `what it means when ${outcome}`;
}
