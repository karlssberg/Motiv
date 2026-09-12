import { Modal } from './Modal.js';

/**
 * The question asked before a document with unsaved changes is closed.
 *
 * A dialog rather than a menu item: discarding is the one destructive act in the shell, and it
 * must never be one click from the action beside it (error prevention). The safe answer comes
 * first and so takes focus when the dialog opens — `showModal()` focuses the first focusable
 * descendant — and the destructive one is last and coloured as such.
 */
export function DiscardDialog(props: {
  /** The document the changes belong to, named so the question is about something. */
  name: string;
  onKeep: () => void;
  onSaveAndClose: () => void;
  onDiscard: () => void;
}) {
  return (
    <Modal label="Unsaved changes" className="dialog" onClose={props.onKeep}>
      <div className="dialog-form">
        <h2 className="dialog-title">Discard changes to “{props.name}”?</h2>
        <p className="dialog-text">What you changed since it was opened will be lost.</p>
        <div className="dialog-actions">
          <button type="button" className="btn btn-secondary" onClick={props.onKeep}>Keep editing</button>
          <button type="button" className="btn btn-secondary" onClick={props.onSaveAndClose}>Save & close</button>
          <button type="button" className="btn btn-danger" onClick={props.onDiscard}>Discard changes</button>
        </div>
      </div>
    </Modal>
  );
}
