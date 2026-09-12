/*
 * PROTOTYPE — throwaway. Do not ship.
 *
 * The body of one open tab: the real editor and evaluate panes over that tab's own store, and a
 * strip that is the prototype's answer to "syncing between tabs" — the propositions a rule
 * references, each with the version the workspace currently knows and a marker when that changed
 * since this tab last looked, or is mid-edit in another tab.
 */
import { useEffect, useSyncExternalStore, type ReactNode } from 'react';
import { createValidationController, type RulesApiClient } from '@motiv-rules/core';
import { RuleEditorProvider, useRuleEditor } from '@motiv-rules/react';
import { DocumentTitle } from '../../panes/DocumentTitle.js';
import { EditorPane } from '../../panes/EditorPane.js';
import { EvaluatePane } from '../../panes/EvaluatePane.js';
import { IconRefresh } from '../../shell/icons.js';
import { KindGlyph } from './TabCard.js';
import { referenceStatuses, referencesOf, type OpenDoc, type TabId, type Workspace } from './workspace.js';

function ReferencesStrip(props: { workspace: Workspace; doc: OpenDoc; onOpen: (name: string) => void }) {
  const state = useSyncExternalStore(props.workspace.subscribe, props.workspace.getState);
  const { document } = useRuleEditor(props.doc.store);
  const statuses = referenceStatuses(state, props.doc, referencesOf(document));
  if (statuses.length === 0) return null;
  const changed = statuses.filter((status) => status.changedSinceSeen).length;

  return (
    <div className={changed > 0 ? 'refs-strip refs-strip-changed' : 'refs-strip'}>
      <span className="refs-label">Uses</span>
      <ul>
        {statuses.map((status) => (
          <li key={status.name}>
            <button
              type="button"
              className={
                'ref-tag' + (status.changedSinceSeen ? ' ref-changed' : '') + (status.editingElsewhere ? ' ref-editing' : '')
              }
              title={status.openIn ? 'Open in its tab' : 'Open in a new tab'}
              onClick={() => props.onOpen(status.name)}
            >
              <KindGlyph kind="proposition" size={12} />
              <span className="ref-name">{status.name}</span>
              {status.version !== null && <span className="ref-version">v{status.version}</span>}
              {status.changedSinceSeen && <span className="ref-badge">updated</span>}
              {status.editingElsewhere && <span className="ref-badge ref-badge-editing">editing</span>}
            </button>
          </li>
        ))}
      </ul>
      {changed > 0 && (
        <button type="button" className="ghost ghost-labelled refs-ack" onClick={() => props.workspace.acknowledge(props.doc.id)}>
          <IconRefresh size={13} />Re-evaluate with {changed === 1 ? 'the new version' : `${changed} new versions`}
        </button>
      )}
    </div>
  );
}

/** Wires live validation to this tab's store, as App does for the shared one. */
function Validation(props: { doc: OpenDoc; client: RulesApiClient }) {
  useEffect(
    () => createValidationController(props.doc.store, props.client, { modelType: props.doc.modelType, debounceMs: 300, isAsync: props.doc.isAsync }),
    [props.doc.store, props.client, props.doc.modelType, props.doc.isAsync],
  );
  return null;
}

export function TabDocument(props: {
  workspace: Workspace;
  client: RulesApiClient;
  doc: OpenDoc;
  onOpenProposition: (name: string) => void;
  /**
   * The document's own actions (JSON, Save), drawn in the editor pane's header beside the title —
   * the document's row, not the shell's — so at every width they sit with what they act on.
   */
  actions?: ReactNode | undefined;
}) {
  const { doc } = props;
  if (doc.status === 'failed') {
    return <section className="empty-state" aria-label="Failed to load"><h2>Could not load {doc.name}</h2><p>{doc.error}</p></section>;
  }
  const title = (
    <DocumentTitle
      name={doc.name}
      modelType={doc.modelType}
      version={doc.version}
      note={doc.isCodeDefault ? 'code-defined default (builder starts fresh)' : undefined}
    />
  );
  return (
    <RuleEditorProvider store={doc.store}>
      <Validation doc={doc} client={props.client} />
      {doc.kind === 'rule' && <ReferencesStrip workspace={props.workspace} doc={doc} onOpen={props.onOpenProposition} />}
      <div className="shell-body">
        <EditorPane
          client={props.client}
          documentName={doc.name}
          title={title}
          actions={props.actions}
        />
        <div className="rail">
          <EvaluatePane client={props.client} />
        </div>
      </div>
    </RuleEditorProvider>
  );
}

export type { TabId };
