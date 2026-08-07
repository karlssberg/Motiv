import { useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';
import { Modal } from '../shell/Modal.js';

/**
 * The live document, as JSON, in a modal.
 *
 * It used to be a third standing column beside the editor and the evaluator. Behind the toolbar
 * both pages get that column back, and nothing is lost: it is one click or one keystroke away, and
 * still live — the same store, re-rendered as it is edited.
 */
export function DocumentModal(props: { onClose: () => void }) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);

  return (
    // No `className`: unlike `.palette` and `.dialog` this modal wants nothing beyond `.modal`'s
    // own surface and measure, and a class no rule defines is a hook that reads as styling.
    <Modal label="Document" onClose={props.onClose} fullscreenOnMobile>
      <h2 className="modal-title">Document<span className="pane-badge">read-only · live</span></h2>
      <div className="modal-body">
        <pre aria-label="rule document" className="json">{JSON.stringify(state.document, null, 2)}</pre>
        {state.errors.length > 0 && (
          <ul aria-label="validation errors" className="errors">
            {state.errors.map((error, i) => (
              <li key={`${error.path}-${i}`} role="alert" className="error">
                {error.code} at {error.path}: {error.message}
              </li>
            ))}
          </ul>
        )}
      </div>
    </Modal>
  );
}
