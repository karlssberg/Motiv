import { useCallback, useEffect, useState } from 'react';
import type {
  DependentEntry, PropositionListEntry, PropositionSaveResult, RulesApiClient,
} from '@motiv/rules-core';
import { useRuleEditor, useRuleEditorStore } from '@motiv/rules-react';
import type { Page } from '../routing/useHashRoute.js';
import { MODEL_TYPE } from '../App.js';
import { AppBar } from './AppBar.js';
import { EditorPane } from './EditorPane.js';
import { JsonPane } from './JsonPane.js';
import { EvaluatePane } from './EvaluatePane.js';
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
 * The propositions page: the namespaced explorer alongside the same Editor / JSON / Evaluate panes
 * the rules page uses. The panes are reused unmodified — they read from the shared RuleEditorStore
 * and never ask what the document represents, so a proposition and a rule are the same thing to them.
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
  // Bumped when the selected name still names something, but something *different* — a revert
  // being the case. The route cannot express that, since the name did not change.
  const [reloads, setReloads] = useState(0);

  const refresh = useCallback(async (): Promise<void> => {
    setEntries(await props.client.listPropositions());
  }, [props.client]);

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
      const [proposition, affected] = await Promise.all([
        props.client.getProposition(name),
        props.client.getDependents(name),
      ]);
      if (cancelled) return;
      setDependents(affected);
      setLoaded({ name, version: proposition.version });
      if (proposition.document) store.loadDocument(proposition.document);
    })();

    return () => { cancelled = true; };
  }, [props.client, props.selected, store, reloads]);

  const modelTypes = [...new Set(entries.map((entry) => entry.modelType))].sort();
  const defaultModelType = modelTypes[0] ?? MODEL_TYPE;

  const save = async (): Promise<void> => {
    if (!loaded) return;
    setSaving(true);
    try {
      const result = await props.client.putProposition(loaded.name, state.document, loaded.version);
      setFailure(describeFailure(result));
      if (result.outcome === 'saved') {
        setLoaded({ ...loaded, version: result.version });
        await refresh();
      }
    } finally {
      setSaving(false);
    }
  };

  const remove = async (entry: PropositionListEntry): Promise<void> => {
    // DELETE answers the same `{ version: 0 }` whether it reverted an override or removed an
    // authored proposition outright, so which one is about to happen is read off the entry
    // *before* the call — afterwards there is nothing in the response to tell them apart.
    const reverts = entry.origin === 'Overridden';
    const result = await props.client.deleteProposition(entry.name, entry.version);
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
    const result = await props.client.createProposition({
      ...values,
      document: { rule: { spec: startsFrom } },
    });

    if (result.outcome !== 'saved') {
      setDialogError(describeFailure(result));
      return;
    }

    setDialog(null);
    setDialogError(null);
    await refresh();
    props.onSelect(values.name);
  };

  const openDialog = (seed: DialogSeed): void => {
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
            {/* Version 0 is the contract's "purely compiled": no overlay document exists for a PUT
                to update, and `baseVersion` must be positive, so Save could only ever fail there.
                Authoring one is what Override is for. */}
            <button
              type="button"
              className="btn"
              disabled={!loaded || loaded.version === 0 || saving}
              onClick={() => void save()}
            >
              Save{dependents.length > 0 ? ` (${dependents.length})` : ''}
            </button>
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

      <div className="shell-body with-rail">
        <PropositionExplorer
          entries={entries}
          selected={props.selected}
          actions={{
            onSelect: props.onSelect,
            onDerive: (name) => openDialog({
              // Prefilled to the source's namespace, so a derivation lands beside its origin.
              name: name.includes('.') ? `${name.slice(0, name.lastIndexOf('.'))}.` : '',
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
        <EditorPane client={props.client} />
        <JsonPane />
        <EvaluatePane client={props.client} />
      </div>

      {dialog && (
        <PropositionDialog
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
