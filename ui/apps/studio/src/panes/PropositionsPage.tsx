import { useEffect, useState } from 'react';
import type { RulesApiClient } from '@motiv-rules/core';
import { whyPropositionSaveUnavailable } from '@motiv-rules/core/workflow';
import { useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';
import { usePropositionWorkflow } from '@motiv-rules/react/workflow';
import type { Page } from '../routing/useHashRoute.js';
import { MODEL_TYPE } from '../App.js';
import { AppBar } from './AppBar.js';
import { DocumentTitle } from './DocumentTitle.js';
import { EditorPane } from './EditorPane.js';
import { EvaluatePane } from './EvaluatePane.js';
import { DocumentModal } from './DocumentModal.js';
import { ReportBanner } from '../shell/ReportBanner.js';
import { DocumentActions } from '../shell/DocumentActions.js';
import { useCommandKey } from '../shell/useCommandKey.js';
import { rememberSelection } from '../shell/lastSelection.js';
import { IconNew, IconOpen } from '../shell/icons.js';
import { PropositionExplorer } from '../explorer/PropositionExplorer.js';
import { PropositionDialog, type DialogSeed, type DialogValues } from '../explorer/PropositionDialog.js';
import { DependentsStrip } from '../explorer/DependentsStrip.js';

/**
 * The namespace a derivation of `name` should land in: everything up to and including the final
 * dot, or the empty string when the name has no namespace to keep.
 */
function namespacePrefixOf(name: string): string {
  const cut = name.lastIndexOf('.');
  return cut < 0 ? '' : name.slice(0, cut + 1);
}

/**
 * A dotted proposition name as the trail it already is: one span per segment, separated by the dots
 * the name is written with, the last one current. It needs no breadcrumb built around it.
 */
function NameTrail(props: { name: string }) {
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
 * The propositions page: the same Editor / Evaluate panes the rules page uses, with the namespaced
 * explorer behind the toolbar as a command palette and the document behind it as a modal. The panes
 * are reused unmodified — they read from the shared RuleEditorStore and never ask what the document
 * represents, so a proposition and a rule are the same thing to them. The save/delete/create loop
 * itself — with its blast-radius reporting and stale-continuation guards — is
 * `PropositionWorkflowController`'s; this renders it and wires the route to its selection.
 */
export function PropositionsPage(props: {
  client: RulesApiClient;
  page: Page;
  selected: string | null;
  onSelect: (name: string | null) => void;
}) {
  const store = useRuleEditorStore();
  const { dirty } = useRuleEditor(store);
  const {
    entries, loaded, dependents, failure, saving,
    refreshEntries, select, reload, save, remove, create,
  } = usePropositionWorkflow(props.client, store, { onSelect: props.onSelect });
  const [dialog, setDialog] = useState<DialogSeed | null>(null);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [explorerOpen, setExplorerOpen] = useState(false);
  const [documentOpen, setDocumentOpen] = useState(false);

  // The same shortcut the rules page opens its palette with — one implementation, so the two
  // cannot drift into meaning different things.
  useCommandKey(() => setExplorerOpen(true));

  // `refreshEntries` and `select` are stable per (client, store) binding, so the listing loads
  // once per server world and the selection follows the route: a deep link and a click take
  // exactly the same path.
  useEffect(() => { void refreshEntries(); }, [refreshEntries]);
  useEffect(() => { void select(props.selected); }, [select, props.selected]);
  // Remembered for the page switch to bring back — including its absence, so a proposition that
  // was deselected (deleted, say) is not what the Propositions link reopens.
  useEffect(() => { rememberSelection('propositions', props.selected); }, [props.selected]);

  // The alphabetically first model type in the listing: what a New starts on, and what stands in
  // for an entry the listing has not got. Not de-duplicated first, since only the first is read.
  const defaultModelType = entries.map((entry) => entry.modelType).sort()[0] ?? MODEL_TYPE;

  // Every flow creates the same shape: a reference to one spec that already exists. UI-authored
  // propositions are composition-only, so there is no emptier document to start from — and reading
  // the editor's draft instead would make what gets created depend on which page was opened first.
  const createFromDialog = async ({ startsFrom, ...values }: DialogValues): Promise<void> => {
    const refused = await create({
      ...values,
      document: { rule: { spec: startsFrom } },
    });
    if (refused !== null) {
      // Reported in the dialog rather than the page banner: the form still holds the input that
      // failed, and closing it over an error would throw that input away.
      setDialogError(refused);
      return;
    }
    setDialog(null);
    setDialogError(null);
  };

  /**
   * Opens New / Derive / Override, dismissing the palette they were reached from. Two stacked
   * modals would leave the browser's focus trap holding the wrong one — and the palette has served
   * its purpose the moment one of its actions has been taken.
   */
  const openDialog = (seed: DialogSeed): void => {
    setExplorerOpen(false);
    setDialogError(null);
    setDialog(seed);
  };

  const modelTypeOf = (name: string): string =>
    entries.find((candidate) => candidate.name === name)?.modelType ?? defaultModelType;

  return (
    <>
      <AppBar
        page={props.page}
        controls={
          <DocumentActions
            kind="proposition"
            name={loaded?.name ?? null}
            dirty={dirty}
            saveUnavailable={whyPropositionSaveUnavailable({ loaded, saving })}
            // The blast radius rides on the label, so what a save would affect is legible from the
            // control that would cause it without reading the strip.
            {...(dependents.length > 0 ? { saveDetail: `(${dependents.length})` } : {})}
            onOpen={() => setExplorerOpen(true)}
            onJson={() => setDocumentOpen(true)}
            onSave={save}
            onClose={() => props.onSelect(null)}
            onDiscard={() => store.revert()}
          />
        }
      />

      {failure !== null && (
        <ReportBanner {...(loaded ? { onReload: () => void reload() } : {})}>
          {failure}
        </ReportBanner>
      )}

      <DependentsStrip dependents={dependents} />

      {/*
        The editor store is shared with the rules page, so while nothing is selected it still holds
        whatever was last edited there — a document this page cannot save. Showing it under a
        "nothing open" title would be a contradiction, so the panes wait for a selection and the
        page says what to do instead. Both actions are the toolbar's, one click nearer.
      */}
      {loaded ? (
        <div className="shell-body">
          <EditorPane
            client={props.client}
            documentName={loaded.name}
            title={
              <DocumentTitle
                name={<NameTrail name={loaded.name} />}
                modelType={modelTypeOf(loaded.name)}
                version={loaded.version}
              />
            }
          />
          <div className="rail">
            <EvaluatePane client={props.client} />
          </div>
        </div>
      ) : (
        <section className="empty-state" aria-label="No proposition open">
          <h2>No proposition open</h2>
          <p>Choose one from the listing, or start a new one composed from the registered specs.</p>
          <div className="run-row">
            <button type="button" className="btn" onClick={() => setExplorerOpen(true)}>
              <IconOpen size={14} />Choose a proposition<kbd aria-hidden="true">⌘K</kbd>
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => openDialog({
                name: '', modelType: defaultModelType, startsFrom: null, title: 'New proposition',
              })}
            >
              <IconNew size={14} />New proposition
            </button>
          </div>
        </section>
      )}

      {explorerOpen && (
        <PropositionExplorer
          entries={entries}
          selected={props.selected}
          actions={{
            onSelect: props.onSelect,
            onClose: () => setExplorerOpen(false),
            onDerive: (name) => openDialog({
              // Prefilled to the source's namespace, so a derivation lands beside its origin.
              name: namespacePrefixOf(name),
              modelType: modelTypeOf(name),
              startsFrom: name,
              title: `Derive from ${name}`,
            }),
            onOverride: (name) => openDialog({
              // An override is authored under the compiled spec's *own* name — POST against a name
              // that exists only as a compiled spec is what mints the overlay entry. So the name is
              // prefilled whole, and `startsFrom` stays null: referencing the name being defined
              // would be a cycle straight back onto itself, so the dialog asks which of the model's
              // *other* specs to compose the replacement from.
              name,
              modelType: modelTypeOf(name),
              startsFrom: null,
              title: `Override ${name}`,
            }),
            onNew: () => openDialog({
              name: '',
              modelType: defaultModelType,
              startsFrom: null,
              title: 'New proposition',
            }),
            onDelete: (entry) => void remove(entry),
          }}
        />
      )}

      {documentOpen && <DocumentModal onClose={() => setDocumentOpen(false)} />}

      {dialog && (
        <PropositionDialog
          // Keyed so that replacing the seed remounts rather than reuses: the dialog seeds its
          // fields from the seed once and never resyncs, so a reused instance would show the new
          // flow's heading over the previous flow's answers — including its `startsFrom`, which
          // the create would then send.
          //
          // Defence in depth, and deliberately kept as such. No route reaches it any more: every
          // flow is opened from the palette, `openDialog` dismisses the palette on the way in, and
          // `onCancel` unmounts this — while ⌘K, which used to reopen the palette stacked above an
          // open dialog, is now inert whenever a modal is showing (`useCommandKey`). The keying
          // costs one prop and survives whoever reopens that route.
          key={dialog.title}
          seed={dialog}
          sources={entries}
          error={dialogError}
          onCancel={() => { setDialog(null); setDialogError(null); }}
          onCreate={(values) => void createFromDialog(values)}
        />
      )}
    </>
  );
}
