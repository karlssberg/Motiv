import { useCallback, useEffect, useRef, useState } from 'react';
import type {
  DependentEntry, PropositionListEntry, PropositionSaveResult, RulesApiClient,
} from '@motiv/rules-core';
import { useRuleEditor, useRuleEditorStore } from '@motiv/rules-react';
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

/** The loaded proposition's server identity: what Save must send back to avoid clobbering. */
interface Loaded {
  name: string;
  version: number;
}

/** Renders a save failure as something a person can act on. */
function describeFailure(result: PropositionSaveResult): string | null {
  switch (result.outcome) {
    case 'saved':
      return null;
    case 'conflict':
      return `Someone else saved version ${result.currentVersion}. Reload before saving again.`;
    case 'nameTaken':
      return 'A proposition is already authored under that name.';
    case 'referenced':
      return `Still referenced by ${result.referrers.join(', ')}. Change those first.`;
    case 'invalid': {
      // Broken dependents are reported apart from document errors, because a document error's path
      // points into *this* document and cannot address a break somewhere else.
      const broken = result.brokenDependents.map((dependent) =>
        `${dependent.kind} ${dependent.name} (${dependent.errors.map((error) => error.message).join('; ')})`);
      return broken.length > 0
        ? `This change would break ${broken.join(', ')}.`
        : result.errors.map((error) => error.message).join('; ');
    }
  }
}

/**
 * Renders a *thrown* failure — everything `describeFailure` cannot see. The typed outcomes cover
 * the refusals the API models; a 500, a 404, or a body that will not parse arrives as a thrown
 * `RulesApiError` instead, and without this it would reach nobody: the page would simply do
 * nothing, which is indistinguishable from the request never having been made.
 */
function describeThrown(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

/**
 * The namespace a derivation of `name` should land in: everything up to and including the final
 * dot, or the empty string when the name has no namespace to keep.
 */
function namespacePrefixOf(name: string): string {
  const cut = name.lastIndexOf('.');
  return cut < 0 ? '' : name.slice(0, cut + 1);
}

/**
 * Why Save cannot run, or `undefined` when it can.
 *
 * Version 0 is the contract's "purely compiled": no overlay document exists for a PUT to update,
 * and `baseVersion` must be positive, so Save could only ever fail there. Authoring one is what
 * Override is for.
 */
function whyNotSave(loaded: Loaded | null, saving: boolean): string | undefined {
  if (loaded === null) return 'Nothing loaded yet.';
  if (loaded.version === 0) return 'This name is served by a compiled spec. Use Override to author one.';
  if (saving) return 'Saving…';
  return undefined;
}

/**
 * The propositions page: the same Editor / Evaluate panes the rules page uses, with the namespaced
 * explorer behind the toolbar as a command palette and the document behind it as a modal. The panes
 * are reused unmodified — they read from the shared RuleEditorStore and never ask what the document
 * represents, so a proposition and a rule are the same thing to them.
 */
export function PropositionsPage(props: {
  client: RulesApiClient;
  page: Page;
  selected: string | null;
  onNavigate: (page: Page) => void;
  onSelect: (name: string | null) => void;
}) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);
  const [entries, setEntries] = useState<PropositionListEntry[]>([]);
  const [loaded, setLoaded] = useState<Loaded | null>(null);
  const [dependents, setDependents] = useState<DependentEntry[]>([]);
  const [failure, setFailure] = useState<string | null>(null);
  const [dialog, setDialog] = useState<DialogSeed | null>(null);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [explorerOpen, setExplorerOpen] = useState(false);
  const [documentOpen, setDocumentOpen] = useState(false);
  // Bumped when the selected name still names something, but something *different* — a revert
  // being the case. The route cannot express that, since the name did not change.
  const [reloads, setReloads] = useState(0);

  // What the page is *currently* about, readable from an async continuation. A continuation closes
  // over the selection as it was when the request went out, so it cannot tell on its own whether
  // the answer it is holding still describes what is on screen. Nothing else disables the tree
  // while a request is in flight, so a click during one is ordinary use, not a race to be ignored.
  const selectedRef = useRef(props.selected);
  useEffect(() => { selectedRef.current = props.selected; }, [props.selected]);

  // The same shortcut the rules page opens its palette with — one implementation, so the two
  // cannot drift into meaning different things.
  useCommandKey(() => setExplorerOpen(true));

  const loadEntries = useCallback(async (): Promise<void> => {
    setEntries(await props.client.listPropositions());
  }, [props.client]);

  /**
   * `loadEntries`, with a failed reload reported in the page banner. Every caller but one wants
   * this: the listing is reloaded on the back of some other act, so a reload that fails has to
   * say so on its own behalf or nothing says it at all. `save` is the exception — its reload runs
   * inside a continuation that decides from `selectedRef` whether reporting is still honest, so it
   * awaits `loadEntries` directly and lets that decision have the last word.
   */
  const refresh = useCallback(async (): Promise<void> => {
    try {
      await loadEntries();
    } catch (error: unknown) {
      setFailure(describeThrown(error));
    }
  }, [loadEntries]);

  useEffect(() => { void refresh(); }, [refresh]);

  // Loading is keyed on the route, so a deep link and a click take exactly the same path.
  useEffect(() => {
    let cancelled = false;
    const name = props.selected;

    // Both of these are claims about whatever was selected a moment ago. Left standing they would
    // read as claims about the incoming selection, and be wrong — so they go before the fetch, not
    // when it lands. `loaded` is not cleared with them: it is replaced wholesale on arrival, and
    // blanking the breadcrumb for one round trip buys nothing.
    setFailure(null);
    setDependents([]);

    if (name === null) {
      setLoaded(null);
      return;
    }

    void (async () => {
      try {
        const [proposition, affected] = await Promise.all([
          props.client.getProposition(name),
          props.client.getDependents(name),
        ]);
        if (cancelled) return;
        setDependents(affected);
        setLoaded({ name, version: proposition.version });
        if (proposition.document) store.loadDocument(proposition.document);
      } catch (error: unknown) {
        if (cancelled) return;
        setFailure(describeThrown(error));
        // `loaded` is deliberately left standing while a load is *in flight* — see above — but once
        // the load has failed there is nothing coming to replace it, and a stale breadcrumb would
        // go on naming a proposition the route no longer points at.
        setLoaded(null);
      }
    })();

    return () => { cancelled = true; };
  }, [props.client, props.selected, store, reloads]);

  // The alphabetically first model type in the listing: what a New starts on, and what stands in
  // for an entry the listing has not got. Not de-duplicated first, since only the first is read.
  const defaultModelType = entries.map((entry) => entry.modelType).sort()[0] ?? MODEL_TYPE;

  const save = async (): Promise<void> => {
    if (!loaded) return;
    const saved = loaded;
    setSaving(true);
    try {
      const result = await props.client.putProposition(saved.name, state.document, saved.version);
      // Everything below is a claim about `saved`. If the selection has moved on, those claims
      // would land on whatever is showing now and be false of it — a version badge naming a save
      // the visible document never had, or a banner blaming it for another's conflict.
      if (selectedRef.current !== saved.name) return;
      setFailure(describeFailure(result));
      if (result.outcome === 'saved') {
        setLoaded({ ...saved, version: result.version });
        await loadEntries();
      }
    } catch (error: unknown) {
      // Covers the reload as well as the PUT, and deliberately: this is the one place where a
      // failed reload is reported only while the selection it followed is still on screen.
      if (selectedRef.current === saved.name) setFailure(describeThrown(error));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (entry: PropositionListEntry): Promise<void> => {
    // DELETE answers the same `{ version: 0 }` whether it reverted an override or removed an
    // authored proposition outright, so which one is about to happen is read off the entry
    // *before* the call — afterwards there is nothing in the response to tell them apart.
    const reverts = entry.origin === 'Overridden';
    let result: PropositionSaveResult;
    try {
      result = await props.client.deleteProposition(entry.name, entry.version);
    } catch (error: unknown) {
      if (selectedRef.current === entry.name) setFailure(describeThrown(error));
      return;
    }
    // As with save: a delete's outcome describes the entry it was aimed at, and navigating on its
    // behalf would drag the user off whatever they moved to while it was in flight.
    if (selectedRef.current !== entry.name) return;
    setFailure(describeFailure(result));
    if (result.outcome !== 'saved') return;
    await refresh();

    if (reverts) {
      // The name survives — it is served by the compiled spec now — so the selection stands and
      // only what sits behind it needs fetching again.
      setReloads((count) => count + 1);
      props.onSelect(entry.name);
      return;
    }
    props.onSelect(null);
  };

  // Every flow creates the same shape: a reference to one spec that already exists. UI-authored
  // propositions are composition-only, so there is no emptier document to start from — and reading
  // the editor's draft instead would make what gets created depend on which page was opened first.
  const create = async ({ startsFrom, ...values }: DialogValues): Promise<void> => {
    let result: PropositionSaveResult;
    try {
      result = await props.client.createProposition({
        ...values,
        document: { rule: { spec: startsFrom } },
      });
    } catch (error: unknown) {
      // Reported in the dialog rather than the page banner: the form still holds the input that
      // failed, and closing it over an error would throw that input away.
      setDialogError(describeThrown(error));
      return;
    }

    if (result.outcome !== 'saved') {
      setDialogError(describeFailure(result));
      return;
    }

    setDialog(null);
    setDialogError(null);
    await refresh();
    // Unguarded on purpose, unlike save and remove: a create is the user's own last explicit act,
    // taken in a modal dialog that covers the explorer, so selecting what they just made is the
    // outcome they asked for rather than a stale continuation overtaking a newer choice.
    props.onSelect(values.name);
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
                unavailable: whyNotSave(loaded, saving),
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
            <button type="button" className="btn" onClick={() => setReloads((count) => count + 1)}>
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
          // the create would then send. Robust whether or not the buttons that can do that stay
          // reachable behind the backdrop.
          key={dialog.title}
          seed={dialog}
          sources={entries}
          error={dialogError}
          onCancel={() => { setDialog(null); setDialogError(null); }}
          onCreate={(values) => void create(values)}
        />
      )}
    </>
  );
}
