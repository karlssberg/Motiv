import { useEffect, useState } from 'react';
import type { RulesApiClient } from '@motiv-rules/core';
import { whyPropositionSaveUnavailable } from '@motiv-rules/core/workflow';
import { useRuleEditorStore } from '@motiv-rules/react';
import { usePropositionWorkflow } from '@motiv-rules/react/workflow';
import type { Page } from '../routing/useHashRoute.js';
import { MODEL_TYPE } from '../App.js';
import { AppBar } from './AppBar.js';
import { EditorPane } from './EditorPane.js';
import { EvaluatePane } from './EvaluatePane.js';
import { DocumentModal } from './DocumentModal.js';
import { Toolbar } from '../shell/Toolbar.js';
import { useCommandKey } from '../shell/useCommandKey.js';
import { IconJson, IconOpen, IconSave } from '../shell/icons.js';
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
  onNavigate: (page: Page) => void;
  onSelect: (name: string | null) => void;
}) {
  const store = useRuleEditorStore();
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

  const segments = loaded?.name.split('.') ?? [];

  return (
    <>
      <AppBar
        page={props.page}
        onNavigate={props.onNavigate}
        controls={
          <>
            {loaded && <span className="rule-version">v{loaded.version}</span>}
            <Toolbar actions={[
              { id: 'open', label: 'Open', icon: IconOpen, onActivate: () => setExplorerOpen(true) },
              {
                // The blast radius rides on the label, so what a save would affect is legible from
                // the control that would cause it without reading the strip.
                id: 'save',
                label: `Save${dependents.length > 0 ? ` (${dependents.length})` : ''}`,
                icon: IconSave,
                onActivate: () => void save(),
                unavailable: whyPropositionSaveUnavailable({ loaded, saving }),
              },
              { id: 'json', label: 'JSON', icon: IconJson, onActivate: () => setDocumentOpen(true) },
            ]} />
          </>
        }
      >
        <span className="breadcrumb-sep">/</span>
        <span className="breadcrumb-item">Propositions</span>
        {/* A dotted name is already a path, so it renders as the trail rather than needing one. */}
        {segments.map((segment, index) => (
          <span key={`${segment}-${index}`}>
            <span className="breadcrumb-sep">/</span>
            <span className={index === segments.length - 1 ? 'breadcrumb-current' : 'breadcrumb-item'}>
              {segment}
            </span>
          </span>
        ))}
      </AppBar>

      {failure !== null && (
        <div role="alert" className="conflict-banner">
          {failure}
          {loaded && (
            <button type="button" className="btn" onClick={() => void reload()}>
              Reload latest
            </button>
          )}
        </div>
      )}

      <DependentsStrip dependents={dependents} />

      <div className="shell-body">
        <EditorPane client={props.client} />
        <EvaluatePane client={props.client} />
      </div>

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
