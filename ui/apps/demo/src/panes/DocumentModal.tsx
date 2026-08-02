import { useRuleEditor, useRuleEditorStore } from '@motiv/rules-react';
import { Modal } from '../shell/Modal.js';

/**
 * The live document, as JSON, in a modal.
 *
 * The same content `JsonPane` used to render beside the editor. Moving it behind the toolbar
 * gives both pages the column back; nothing is lost, because it is one keystroke away.
 */
export function DocumentModal(props: { onClose: () => void }) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);

  return (
    <Modal label="Document" onClose={props.onClose} className="document-modal" fullscreenOnMobile>
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
