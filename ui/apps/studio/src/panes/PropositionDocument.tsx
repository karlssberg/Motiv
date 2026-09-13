import { useCallback, useEffect, useRef, useState, useSyncExternalStore } from 'react';
import { createValidationController, type RulesApiClient } from '@motiv-rules/core';
import { whyPropositionSaveUnavailable } from '@motiv-rules/core/workflow';
import { RuleEditorProvider, useRuleEditor } from '@motiv-rules/react';
import { usePropositionWorkflow } from '@motiv-rules/react/workflow';
import { MODEL_TYPE } from '../App.js';
import { DocumentTitle } from './DocumentTitle.js';
import { EditorPane } from './EditorPane.js';
import { EvaluatePane } from './EvaluatePane.js';
import { DocumentModal } from './DocumentModal.js';
import { ReportBanner } from '../shell/ReportBanner.js';
import { DocActions } from '../shell/DocActions.js';
import { DependentsStrip } from '../explorer/DependentsStrip.js';
import { placeActions, useNoteSaves, useSaverRegistration, type SaverSink } from './documentTab.js';
import type { OpenDoc, Workspace } from '../shell/workspace.js';

/**
 * A dotted proposition name as the trail it already is: one span per segment, separated by the dots
 * the name is written with, the last one current. It needs no breadcrumb built around it.
 */
export function NameTrail(props: { name: string }) {
  const segments = props.name.split('.');
  return (
    <>
      {segments.map((segment, index) => (
        <span key={`${segment}-${index}`}>
          {index > 0 && <span className="breadcrumb-sep">.</span>}
          <span className={index === segments.length - 1 ? 'breadcrumb-current' : 'breadcrumb-item'}>
            {segment}
          </span>
        </span>
      ))}
    </>
  );
}

/**
 * One open proposition: the same Editor / Evaluate panes a rule gets, over the tab's own store,
 * with the blast radius above them. The panes are reused unmodified — they read from the store
 * they are given and never ask what the document represents. The load / save loop, with its
 * blast-radius reporting and stale-continuation guards, is `PropositionWorkflowController`'s,
 * bound to this store; the listing and its authoring actions (New, Derive, Override, Delete) are
 * the shell's, because they act on the set rather than on a tab.
 *
 * Stays mounted while its tab is hidden, so the draft survives a switch; loads once, on mount.
 */
export function PropositionDocument(props: {
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
}) {
  const { client, tab, workspace } = props;
  const { entries, loaded, dependents, failure, saving, refreshEntries, select, reload, save } =
    usePropositionWorkflow(client, tab.store);
  const [documentOpen, setDocumentOpen] = useState(false);

  useSaverRegistration(props.onSaver, save);

  // `refreshEntries` and `select` are stable per (client, store) binding, so the listing loads
  // once per server world and the document once per tab.
  useEffect(() => { void refreshEntries(); }, [refreshEntries]);
  useEffect(() => { void select(tab.name); }, [select, tab.name]);
  // A delete that reverted this proposition to its compiled definition happened in the shell's
  // workflow, not this one, so the shell asks for the reload rather than this noticing it.
  useEffect(() => { if (tab.reloads > 0) void reload(); }, [reload, tab.reloads]);
  // The set moved — a save, a create or a delete elsewhere — so the blast radius may have too. A
  // clean tab takes the server's word again; a dirty one keeps its draft and its strip goes stale
  // until it is saved, which is the lesser wrong.
  const revision = useSyncExternalStore(workspace.subscribe, () => workspace.getState().revision);
  const { dirty } = useRuleEditor(tab.store);
  const seenRevision = useRef(revision);
  useEffect(() => {
    if (revision === seenRevision.current) return;
    seenRevision.current = revision;
    if (!dirty) void select(tab.name);
  }, [revision, dirty, select, tab.name]);

  // The listing's model type for this name, else the alphabetically first in scope — what stands
  // in for an entry the listing has not got.
  const modelType = entries.find((entry) => entry.name === tab.name)?.modelType
    ?? entries.map((entry) => entry.modelType).sort()[0]
    ?? MODEL_TYPE;

  useEffect(
    () => createValidationController(tab.store, client, { modelType, debounceMs: 300, isAsync: false }),
    [tab.store, client, modelType],
  );

  // Its own bump: what this tab holds *is* the server's, so there is nothing to reload.
  const rebaseline = useCallback(() => {
    seenRevision.current = workspace.getState().revision;
  }, [workspace]);
  useNoteSaves(workspace, 'proposition', loaded, rebaseline);

  return (
    <RuleEditorProvider store={tab.store}>
      {failure !== null && (
        <ReportBanner {...(loaded ? { onReload: () => void reload() } : {})}>
          {failure}
        </ReportBanner>
      )}

      <DependentsStrip dependents={dependents} />

      <div className="shell-body">
        <EditorPane
          client={client}
          documentName={tab.name}
          title={
            <DocumentTitle
              name={<NameTrail name={tab.name} />}
              modelType={modelType}
              version={loaded?.version}
            />
          }
          actions={placeActions(props.actionsHost,
            <DocActions
              saveUnavailable={whyPropositionSaveUnavailable({ loaded, saving })}
              // The blast radius rides on the label, so what a save would affect is legible from
              // the control that would cause it without reading the strip.
              {...(dependents.length > 0 ? { saveDetail: `(${dependents.length})` } : {})}
              onJson={() => setDocumentOpen(true)}
              onSave={save}
              onClose={props.onClose}
            />,
          )}
        />
        <div className="rail">
          <EvaluatePane client={client} />
        </div>
      </div>

      {documentOpen && <DocumentModal onClose={() => setDocumentOpen(false)} />}
    </RuleEditorProvider>
  );
}
