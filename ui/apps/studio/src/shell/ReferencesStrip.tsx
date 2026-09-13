import { useSyncExternalStore } from 'react';
import { useRuleEditor } from '@motiv-rules/react';
import { IconCheck, IconPropositions } from './icons.js';
import { referenceStatuses, referencesOf, type OpenDoc, type Workspace } from './workspace.js';

/**
 * The propositions a rule references, each with the version the workspace knows — and how tabs
 * stay in sync. A reference is marked **editing** while another tab holds it with unsaved changes
 * (a change that is coming) and **updated** once a save landed after this tab last looked (a
 * change that has). *Got it* re-baselines; Evaluate already runs against the server, which has
 * the new version, so nothing else needs refreshing.
 *
 * Reads the tab's own store for the document, so it follows every edit, and the workspace for
 * everyone else's state. Always rendered for a rule, empty or not: a strip that appeared with the
 * first reference would push the editor's header down under the pointer mid-edit.
 */
export function ReferencesStrip(props: {
  workspace: Workspace;
  doc: OpenDoc;
  /** Opens (or activates) the named proposition's tab. */
  onOpen: (name: string) => void;
}) {
  const state = useSyncExternalStore(props.workspace.subscribe, props.workspace.getState);
  const { document } = useRuleEditor(props.doc.store);
  // The workspace's copy of the tab, not the prop's: `seenAt` moves on acknowledge, and a host
  // that captured the tab when it opened would otherwise keep reporting the change it dismissed.
  const doc = state.docs[props.doc.id] ?? props.doc;
  const statuses = referenceStatuses(state, doc, referencesOf(document));
  const changed = statuses.filter((status) => status.changedSinceSeen).length;

  return (
    <div className={changed > 0 ? 'refs-strip refs-strip-changed' : 'refs-strip'} role="group" aria-label="Uses">
      <span className="refs-label" aria-hidden="true">Uses</span>
      {statuses.length === 0 && <span className="refs-none">no propositions yet</span>}
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
              <span className="kind-glyph kind-proposition"><IconPropositions size={12} /></span>
              <span className="ref-name">{status.name}</span>
              {status.version !== null && <span className="ref-version">v{status.version}</span>}
              {status.changedSinceSeen && <span className="ref-badge">updated</span>}
              {status.editingElsewhere && <span className="ref-badge ref-badge-editing">editing</span>}
            </button>
          </li>
        ))}
      </ul>
      {changed > 0 && (
        <button type="button" className="ghost ghost-labelled refs-ack" onClick={() => props.workspace.acknowledge(doc.id)}>
          <IconCheck size={13} />Got it
        </button>
      )}
    </div>
  );
}
