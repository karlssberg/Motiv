import { useEffect, useRef, type MouseEvent, type ReactNode } from 'react';
import { IconClose } from './icons.js';

/**
 * A modal built on the native `<dialog>` element.
 *
 * `showModal()` gives focus trapping, Escape handling, backdrop inertness and correct assistive-
 * technology semantics with no library and no hand-rolled focus management — which is why this
 * exists rather than another `aria-modal` div. The app previously had one of those, and it had
 * none of those behaviours.
 *
 * Dismissal arrives three ways — the close control, Escape (as a native `cancel` event), and a
 * backdrop click — and all three are reported through the single `onClose`.
 */
export function Modal(props: {
  /** The dialog's accessible name. */
  label: string;
  onClose: () => void;
  className?: string;
  /** When set, the dialog fills the viewport below 900px instead of floating. */
  fullscreenOnMobile?: boolean;
  children: ReactNode;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  const { onClose } = props;

  useEffect(() => {
    const dialog = ref.current;
    if (dialog === null) return;
    // Guarded because React 18 StrictMode runs effects twice, and showModal() on an already-open
    // dialog throws InvalidStateError.
    if (!dialog.open) dialog.showModal();
    return () => { if (dialog.open) dialog.close(); };
  }, []);

  useEffect(() => {
    const dialog = ref.current;
    if (dialog === null) return;
    // Escape reaches a <dialog> as `cancel`, never as a keydown on our own tree, so this is the
    // only place the key can be observed. Prevented so the browser does not also close the
    // dialog behind React's back, leaving the caller's state saying it is still open.
    const cancel = (event: Event): void => { event.preventDefault(); onClose(); };
    dialog.addEventListener('cancel', cancel);
    return () => dialog.removeEventListener('cancel', cancel);
  }, [onClose]);

  // The backdrop belongs to the dialog element itself, so a click on it targets the dialog while
  // a click on any content targets a descendant. Comparing target to currentTarget is what tells
  // them apart — there is no separate backdrop node to listen on.
  const onClick = (event: MouseEvent<HTMLDialogElement>): void => {
    if (event.target === event.currentTarget) onClose();
  };

  const classes = ['modal', props.fullscreenOnMobile === true ? 'modal-mobile-full' : null, props.className]
    .filter((name) => name !== null && name !== undefined)
    .join(' ');

  return (
    <dialog ref={ref} className={classes} aria-label={props.label} onClick={onClick}>
      {props.children}
      {/*
        Last in the document, not first, though it is painted top-right either way — it is
        absolutely positioned, so its place here is about focus rather than layout. `showModal()`
        runs the dialog focusing steps, which hand focus to the first focusable descendant unless
        one carries the `autofocus` *attribute* — and React's `autoFocus` prop is not that
        attribute, it is an imperative `focus()` during commit that `showModal()` then overrides.
        With this button first, every modal in the app opened with focus on Close: the palette's
        caret was not in its search box and the authoring dialog's was not in its Name field,
        whatever their own markup asked for. jsdom's `showModal` shim sets `open` and nothing else,
        so no unit test could see it.
      */}
      <button type="button" className="ghost modal-close" aria-label="Close" title="Close" onClick={onClose}>
        <IconClose size={15} />
      </button>
    </dialog>
  );
}
