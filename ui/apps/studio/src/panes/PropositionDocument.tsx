import { useEffect, useRef, useState } from 'react';
import { createValidationController, type RulesApiClient } from '@motiv-rules/core';
import { whyPropositionSaveUnavailable } from '@motiv-rules/core/workflow';
import { RuleEditorProvider } from '@motiv-rules/react';
import { usePropositionWorkflow } from '@motiv-rules/react/workflow';
import { MODEL_TYPE } from '../App.js';
import { DocumentTitle } from './DocumentTitle.js';
import { EditorPane } from './EditorPane.js';
import { EvaluatePane } from './EvaluatePane.js';
import { DocumentModal } from './DocumentModal.js';
import { ReportBanner } from '../shell/ReportBanner.js';
import { DocActions } from '../shell/DocActions.js';
import { DependentsStrip } from '../explorer/DependentsStrip.js';
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
}) {
  const { client, tab, workspace } = props;
  const { entries, loaded, dependents, failure, saving, refreshEntries, select, reload, save } =
    usePropositionWorkflow(client, tab.store);
  const [documentOpen, setDocumentOpen] = useState(false);

  // `refreshEntries` and `select` are stable per (client, store) binding, so the listing loads
  // once per server world and the document once per tab.
  useEffect(() => { void refreshEntries(); }, [refreshEntries]);
  useEffect(() => { void select(tab.name); }, [select, tab.name]);

  // The listing's model type for this name, else the alphabetically first in scope — what stands
  // in for an entry the listing has not got.
  const modelType = entries.find((entry) => entry.name === tab.name)?.modelType
    ?? entries.map((entry) => entry.modelType).sort()[0]
    ?? MODEL_TYPE;

  useEffect(
    () => createValidationController(tab.store, client, { modelType, debounceMs: 300, isAsync: false }),
    [tab.store, client, modelType],
  );

  // A version that moved *after* the load is a save: the workspace learns it, so a rule tab that
  // uses this proposition can say so.
  const seenVersion = useRef<number | null>(null);
  useEffect(() => {
    if (!loaded) { seenVersion.current = null; return; }
    if (seenVersion.current !== null && loaded.version !== seenVersion.current) {
      workspace.noteSaved('proposition', loaded.name, loaded.version);
    }
    seenVersion.current = loaded.version;
  }, [loaded, workspace]);

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
          actions={
            <DocActions
              saveUnavailable={whyPropositionSaveUnavailable({ loaded, saving })}
              // The blast radius rides on the label, so what a save would affect is legible from
              // the control that would cause it without reading the strip.
              {...(dependents.length > 0 ? { saveDetail: `(${dependents.length})` } : {})}
              onJson={() => setDocumentOpen(true)}
              onSave={save}
              onClose={props.onClose}
            />
          }
        />
        <div className="rail">
          <EvaluatePane client={client} />
        </div>
      </div>

      {documentOpen && <DocumentModal onClose={() => setDocumentOpen(false)} />}
    </RuleEditorProvider>
  );
}
