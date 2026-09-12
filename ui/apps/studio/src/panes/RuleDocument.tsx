import { useEffect, useRef, useState } from 'react';
import { createValidationController, type RulesApiClient } from '@motiv-rules/core';
import { whyRuleSaveUnavailable } from '@motiv-rules/core/workflow';
import { RuleEditorProvider } from '@motiv-rules/react';
import { useRuleWorkflow } from '@motiv-rules/react/workflow';
import { MODEL_TYPE } from '../App.js';
import { DocumentModal } from './DocumentModal.js';
import { DocumentTitle } from './DocumentTitle.js';
import { EditorPane } from './EditorPane.js';
import { EvaluatePane } from './EvaluatePane.js';
import { CheckoutPane } from './CheckoutPane.js';
import { DocActions } from '../shell/DocActions.js';
import { ReportBanner } from '../shell/ReportBanner.js';
import { ReferencesStrip } from '../shell/ReferencesStrip.js';
import type { OpenDoc, Workspace } from '../shell/workspace.js';

/**
 * One open rule: the editor beside a rail of the two panes that *run* it — Evaluate against a
 * sample, and the live checkout the server decides — over the tab's own store. The save loop is
 * `RuleWorkflowController`'s, bound to that store; this renders it, and tells the workspace when a
 * save produced a version so every other tab that references this rule can see it move.
 *
 * Stays mounted while its tab is hidden, so the draft, the undo stack, the surface choice and an
 * unresolved conflict all survive a switch. Loads once, on mount: the route follows the tab, not
 * the other way round, so there is no selection to follow here.
 */
export function RuleDocument(props: {
  client: RulesApiClient;
  tab: OpenDoc;
  workspace: Workspace;
  /** Closes this tab — reached only from a save that landed, so no question is asked. */
  onClose: () => void;
  /** Opens (or activates) a proposition this rule references. */
  onOpenProposition: (name: string) => void;
}) {
  const { client, tab, workspace } = props;
  const { loaded, loadedEntry, conflict, failure, saving, refresh, load, save } =
    useRuleWorkflow(client, tab.store);
  const [documentOpen, setDocumentOpen] = useState(false);

  // `refresh` and `load` are stable per (client, store) binding, so the listing loads once per
  // server world and the document once per tab.
  useEffect(() => { void refresh(); }, [refresh]);
  useEffect(() => { void load(tab.name); }, [load, tab.name]);

  // Live validation for this tab's store. When the loaded rule is async, validation allows async
  // spec references too, so the document may reference them without red herrings.
  const isAsync = loadedEntry?.isAsync ?? false;
  useEffect(
    () => createValidationController(tab.store, client, { modelType: MODEL_TYPE, debounceMs: 300, isAsync }),
    [tab.store, client, isAsync],
  );

  // A version that moved *after* the load is a save: the workspace learns it, so other tabs can.
  const seenVersion = useRef<number | null>(null);
  useEffect(() => {
    if (!loaded) { seenVersion.current = null; return; }
    if (seenVersion.current !== null && loaded.version !== seenVersion.current) {
      workspace.noteSaved('rule', loaded.name, loaded.version);
    }
    seenVersion.current = loaded.version;
  }, [loaded, workspace]);

  return (
    <RuleEditorProvider store={tab.store}>
      {/*
        Reported first because it is always the newer event: only a save records a conflict, and
        every operation clears the failure on its way out — so a failure standing beside a
        conflict was necessarily raised after it.
      */}
      {failure !== null && (
        <ReportBanner {...(loaded ? { onReload: () => void load(loaded.name) } : {})}>
          {failure}
        </ReportBanner>
      )}
      {conflict !== null && loaded && (
        <ReportBanner onReload={() => void load(loaded.name)}>
          Someone else saved version {conflict} of “{loaded.name}”.
        </ReportBanner>
      )}

      <ReferencesStrip workspace={workspace} doc={tab} onOpen={props.onOpenProposition} />

      {/*
        Each pane below fetches GET /catalog on mount (EditorPane and EvaluatePane via useCatalog,
        CheckoutPane directly) — and EditorPane's builder surface fetches once more of its own, so
        up to four requests for the same static payload. Deduping would mean lifting the catalog
        here and passing it down, but each pane's self-contained wiring is a deliberate seam Studio
        exists to show, so the duplicate requests are accepted.
      */}
      <div className="shell-body">
        <EditorPane
          client={client}
          documentName={tab.name}
          title={
            <DocumentTitle
              name={tab.name}
              modelType={MODEL_TYPE}
              version={loaded?.version}
              note={loaded?.isCodeDefault ? 'code-defined default (builder starts fresh)' : undefined}
            />
          }
          actions={
            <DocActions
              saveUnavailable={whyRuleSaveUnavailable({ loaded, saving })}
              onJson={() => setDocumentOpen(true)}
              onSave={save}
              onClose={props.onClose}
            />
          }
        />
        {/*
          One rail for both ways of running the rule: beside Evaluate, Checkout shares one result
          language — a verdict, then the assertions — and the editor keeps the height it needs.
        */}
        <div className="rail">
          <EvaluatePane client={client} />
          <CheckoutPane client={client} />
        </div>
      </div>

      {documentOpen && <DocumentModal onClose={() => setDocumentOpen(false)} />}
    </RuleEditorProvider>
  );
}
