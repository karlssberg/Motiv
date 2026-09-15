import { useEffect, useState } from 'react';
import { createValidationController, type RulesApiClient } from '@motiv-rules/core';
import { whyRuleSaveUnavailable } from '@motiv-rules/core/workflow';
import { RuleEditorProvider } from '@motiv-rules/react';
import { useRuleWorkflow } from '@motiv-rules/react/workflow';
import { MODEL_TYPE } from '../App.js';
import { DocumentModal } from './DocumentModal.js';
import { DocumentTitle } from './DocumentTitle.js';
import { EditorPane } from './EditorPane.js';
import { ScenarioPane } from './ScenarioPane.js';
import { DocActions } from '../shell/DocActions.js';
import { ReportBanner } from '../shell/ReportBanner.js';
import { ReferencesStrip } from '../shell/ReferencesStrip.js';
import { placeActions, useNoteSaves, useSaverRegistration, type SaverSink } from './documentTab.js';
import type { OpenDoc, Workspace } from '../shell/workspace.js';

/**
 * One open rule: the editor beside the pane that *runs* it — every scenario against the live rule
 * and against this tab's draft — over the tab's own store. The save loop is
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
  /**
   * Where to draw the document's actions instead of the editor's header: the app bar's free
   * space, while the tab strip is a dropdown. `null` keeps them in the header.
   */
  actionsHost?: HTMLElement | null | undefined;
  /**
   * Hands the shell this tab's save, so the unsaved-changes question's *Save & close* can run it
   * for a tab that is not the active one. Called with `null` on unmount.
   */
  onSaver?: SaverSink | undefined;
  /** Opens (or activates) a proposition this rule references. */
  onOpenProposition: (name: string) => void;
  /**
   * Opens the shell's *Extract to catalog* dialog for a builder row, and its *Promote to catalog*
   * dialog for a definition — both act on the proposition *set*, so the shell owns them (#234).
   */
  onExtractToCatalog?: ((path: string) => void) | undefined;
  onPromote?: ((name: string) => void) | undefined;
}) {
  const { client, tab, workspace } = props;
  const { loaded, loadedEntry, conflict, failure, saving, refresh, load, save } =
    useRuleWorkflow(client, tab.store);
  const [documentOpen, setDocumentOpen] = useState(false);

  useSaverRegistration(props.onSaver, save);

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

  useNoteSaves(workspace, 'rule', loaded);

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
        Each pane below fetches GET /catalog on mount (EditorPane and ScenarioPane via useCatalog)
        — and EditorPane's builder surface fetches once more of its own, so up to three requests
        for the same static payload. Deduping would mean lifting the catalog
        here and passing it down, but each pane's self-contained wiring is a deliberate seam Studio
        exists to show, so the duplicate requests are accepted.
      */}
      <div className="shell-body">
        <EditorPane
          client={client}
          documentName={tab.name}
          onExtractToCatalog={props.onExtractToCatalog}
          onPromote={props.onPromote}
          title={
            <DocumentTitle
              name={tab.name}
              fullName={tab.name}
              modelType={MODEL_TYPE}
              version={loaded?.version}
              note={loaded?.isCodeDefault ? 'code-defined default (builder starts fresh)' : undefined}
            />
          }
          actions={placeActions(props.actionsHost,
            <DocActions
              saveUnavailable={whyRuleSaveUnavailable({ loaded, saving })}
              onJson={() => setDocumentOpen(true)}
              onSave={save}
              onClose={props.onClose}
            />,
          )}
        />
        {/* One rail: the rule's scenarios, each run against the live rule and this tab's draft. */}
        <div className="rail">
          <ScenarioPane client={client} ruleName={tab.name} version={loaded?.version} />
        </div>
      </div>

      {documentOpen && <DocumentModal onClose={() => setDocumentOpen(false)} />}
    </RuleEditorProvider>
  );
}
