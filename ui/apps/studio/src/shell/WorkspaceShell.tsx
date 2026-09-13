import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react';
import { RuleEditorStore, type PropositionListEntry, type RuleListEntry, type RulesApiClient } from '@motiv-rules/core';
import { useRuleEditor } from '@motiv-rules/react';
import { usePropositionWorkflow } from '@motiv-rules/react/workflow';
import type { Route } from '../routing/useHashRoute.js';
import { AppBar } from '../panes/AppBar.js';
import { RuleDocument } from '../panes/RuleDocument.js';
import { PropositionDocument } from '../panes/PropositionDocument.js';
import { PropositionExplorer } from '../explorer/PropositionExplorer.js';
import { PropositionDialog, type DialogSeed, type DialogValues } from '../explorer/PropositionDialog.js';
import { DiscardDialog } from './DiscardDialog.js';
import { OpenPalette } from './OpenPalette.js';
import { ReportBanner } from './ReportBanner.js';
import { TabStrip } from './TabStrip.js';
import { useCommandKey } from './useCommandKey.js';
import { IconNew, IconOpen } from './icons.js';
import { Workspace, tabIdOf, type DocKind, type OpenDoc, type TabId } from './workspace.js';

/** The page a kind of document is routed under, and back. */
const PAGE_OF: Record<DocKind, 'rules' | 'propositions'> = { rule: 'rules', proposition: 'propositions' };
const KIND_OF = { rules: 'rule', propositions: 'proposition' } as const;

/**
 * The namespace a derivation of `name` should land in: everything up to and including the final
 * dot, or the empty string when the name has no namespace to keep.
 */
function namespacePrefixOf(name: string): string {
  const cut = name.lastIndexOf('.');
  return cut < 0 ? '' : name.slice(0, cut + 1);
}

/** The unsaved-changes question, asked of the tab's own store; closes outright when it is clean. */
function CloseQuestion(props: { doc: OpenDoc; onKeep: () => void; onClose: () => void; onSaveAndClose: () => void }) {
  const { dirty } = useRuleEditor(props.doc.store);
  const { onClose } = props;
  useEffect(() => { if (!dirty) onClose(); }, [dirty, onClose]);
  if (!dirty) return null;
  return (
    <DiscardDialog
      name={props.doc.name}
      onKeep={props.onKeep}
      onSaveAndClose={props.onSaveAndClose}
      onDiscard={() => { props.doc.store.revert(); props.onClose(); }}
    />
  );
}

/**
 * The shell: the app bar with the tab strip, one hidden panel per open document, and everything
 * that acts on the *set* of documents rather than on one — the Open palette, the propositions
 * explorer and its authoring dialog, the close question, ⌘K and ⌘W.
 *
 * The hash route is the truth for which tab is active. A named route opens (or activates) that
 * tab; activating a tab writes its route; closing the active tab navigates to the neighbour the
 * workspace picked, or to the bare page. So a deep link, a chip, the back button and a hand-edited
 * URL all take the same path.
 *
 * The propositions *listing* and its authoring actions run through a workflow bound to a scratch
 * store that never loads a document: create and delete are acts on the listing, and each tab's
 * own workflow handles its document.
 */
export function WorkspaceShell(props: {
  client: RulesApiClient;
  route: Route;
  navigate: (route: Route) => void;
  /** The workspace to run over; by default a fresh one, restored from the session. Injected by tests. */
  workspace?: Workspace;
}) {
  const { client, route, navigate } = props;
  const [workspace] = useState(() => {
    if (props.workspace) return props.workspace;
    const ws = new Workspace();
    ws.restore();
    return ws;
  });
  const state = useSyncExternalStore(workspace.subscribe, workspace.getState);

  // --- the route drives the active tab; nothing here writes the route but a navigation --------
  // One direction only. A chip click and a close both *navigate*, and the workspace follows the
  // route in this effect — so there is no second effect writing the route back, and no commit in
  // which the two can disagree (the first render, before an open has flowed back into React, is
  // exactly such a commit, and a route-writing effect there deactivated the tab it had just opened).
  useEffect(() => {
    if (route.page === 'admin') return;
    if (route.name === null) workspace.activate(null);
    else workspace.open(KIND_OF[route.page], route.name);
  }, [workspace, route.page, route.name]);

  const activeDoc = state.active === null ? null : state.docs[state.active] ?? null;

  const openTab = useCallback((kind: DocKind, name: string): void => {
    navigate({ page: PAGE_OF[kind], name });
  }, [navigate]);

  const activateTab = useCallback((id: TabId): void => {
    const doc = workspace.getState().docs[id];
    if (doc) openTab(doc.kind, doc.name);
  }, [workspace, openTab]);

  // --- the listings, and the propositions workflow that authors against them ----------------
  const [scratch] = useState(() => new RuleEditorStore({ rule: { spec: 'customer.is-active' } }));
  const deleting = useRef<PropositionListEntry | null>(null);
  // `closeTab` is defined below the workflow that needs it; the ref carries the current one.
  const closeTabRef = useRef<(id: TabId) => void>(() => {});
  const { entries, failure, refreshEntries, select, remove, create } = usePropositionWorkflow(client, scratch, {
    // The controller reports where a create or delete leaves the selection; here that is which tab
    // to open, or that the deleted proposition's tab has nothing left to show.
    onSelect: (name) => {
      if (name !== null) { openTab('proposition', name); return; }
      const gone = deleting.current;
      if (gone) closeTabRef.current(tabIdOf('proposition', gone.name));
    },
  });
  const [rules, setRules] = useState<RuleListEntry[]>([]);
  const refreshRules = useCallback(async (): Promise<void> => {
    try { setRules(await client.listRules()); } catch { /* the rule tabs report their own listing failures */ }
  }, [client]);

  // Once on mount, and again after any tab's save, so what the palette and the hover cards say —
  // and what a rule tab's references compare against — is what the server has.
  useEffect(() => { void refreshEntries(); void refreshRules(); }, [refreshEntries, refreshRules, state.saves]);
  useEffect(() => { workspace.setListings(rules, entries); }, [workspace, rules, entries]);

  const removeEntry = async (entry: PropositionListEntry): Promise<void> => {
    // The controller reports and refreshes only for its own selection — its stale-continuation
    // guard — so the entry is selected on the scratch workflow first. It stays selected after:
    // a `select(null)` would clear the failure the delete may just have reported, and nothing
    // renders the scratch selection anyway.
    deleting.current = entry;
    try {
      await select(entry.name);
      await remove(entry);
    } finally {
      deleting.current = null;
    }
    // Still listed after the delete: an override reverted to its compiled definition, which the
    // tab holding it cannot know about.
    const id = tabIdOf('proposition', entry.name);
    if (workspace.getState().docs[id]) workspace.requestReload(id);
  };

  // --- palettes, dialogs, the close question ----------------------------------------------------
  const [palette, setPalette] = useState<'open' | 'explorer' | null>(null);
  const [dialog, setDialog] = useState<DialogSeed | null>(null);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [closing, setClosing] = useState<TabId | null>(null);
  const [compact, setCompact] = useState(false);
  const [actionsHost, setActionsHost] = useState<HTMLElement | null>(null);

  useCommandKey(() => setPalette('open'));
  useEffect(() => {
    const onKey = (event: KeyboardEvent): void => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'w' && state.active !== null) {
        event.preventDefault();
        setClosing(state.active);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [state.active]);

  const defaultModelType = entries.map((entry) => entry.modelType).sort()[0] ?? 'customer';
  const modelTypeOf = (name: string): string =>
    entries.find((candidate) => candidate.name === name)?.modelType ?? defaultModelType;

  /** Opens New / Derive / Override, dismissing the palette they were reached from. */
  const openDialog = (seed: DialogSeed): void => {
    setPalette(null);
    setDialogError(null);
    setDialog(seed);
  };

  const createFromDialog = async ({ startsFrom, ...values }: DialogValues): Promise<void> => {
    const refused = await create({ ...values, document: { rule: { spec: startsFrom } } });
    if (refused !== null) {
      // Reported in the dialog rather than the banner: the form still holds the input that failed.
      setDialogError(refused);
      return;
    }
    setDialog(null);
    setDialogError(null);
  };

  const closingDoc = closing === null ? null : state.docs[closing] ?? null;
  /**
   * Closes a tab. When it was the active one the workspace has picked its neighbour, and the
   * route goes there — or to the bare page when nothing is left; an inactive tab's close leaves
   * the route alone.
   */
  const closeTab = useCallback((id: TabId): void => {
    const wasActive = workspace.getState().active === id;
    workspace.close(id);
    setClosing(null);
    if (!wasActive || route.page === 'admin') return;
    const next = workspace.getState().active;
    const doc = next === null ? null : workspace.getState().docs[next] ?? null;
    navigate(doc ? { page: PAGE_OF[doc.kind], name: doc.name } : { page: route.page, name: null });
  }, [workspace, navigate, route.page]);
  closeTabRef.current = closeTab;

  // Each tab's save, registered by its document: what the close question's Save & close runs.
  const savers = useRef<Record<TabId, () => Promise<boolean>>>({});
  const saverFor = useMemo(() => {
    const cache: Record<TabId, (save: (() => Promise<boolean>) | null) => void> = {};
    return (id: TabId) => (cache[id] ??= (save) => {
      if (save) savers.current[id] = save;
      else delete savers.current[id];
    });
  }, []);
  const saveAndClose = async (id: TabId): Promise<void> => {
    setClosing(null);
    if (await savers.current[id]?.()) closeTab(id);
  };
  const openIds = useMemo(() => new Set(state.tabs), [state.tabs]);

  return (
    <>
      <AppBar
        controls={<div ref={setActionsHost} className="doc-actions-host" />}
      >
        <TabStrip
          workspace={workspace}
          onActivate={activateTab}
          onRequestClose={setClosing}
          onOpen={() => setPalette('open')}
          onCompactChange={setCompact}
        />
      </AppBar>

      {failure !== null && <ReportBanner>{failure}</ReportBanner>}

      {state.tabs.map((id) => {
        const doc = state.docs[id];
        if (!doc) return null;
        const active = id === state.active;
        return (
          <section key={id} className="tab-panel" role="tabpanel" aria-label={doc.name} hidden={!active}>
            {doc.kind === 'rule'
              ? (
                <RuleDocument
                  client={client}
                  tab={doc}
                  workspace={workspace}
                  onClose={() => closeTab(id)}
                  onOpenProposition={(name) => openTab('proposition', name)}
                  actionsHost={active && compact ? actionsHost : null}
                  onSaver={saverFor(id)}
                />
              )
              : (
                <PropositionDocument
                  client={client}
                  tab={doc}
                  workspace={workspace}
                  onClose={() => closeTab(id)}
                  actionsHost={active && compact ? actionsHost : null}
                  onSaver={saverFor(id)}
                />
              )}
          </section>
        );
      })}

      {activeDoc === null && (
        <section className="empty-state" aria-label="Nothing open">
          <h2>Nothing open</h2>
          <p>Open a rule or a proposition in a tab. Several can be open at once, and each keeps its own draft.</p>
          <div className="run-row">
            <button type="button" className="btn" onClick={() => setPalette('open')}>
              <IconOpen size={14} />Open a rule or proposition<kbd aria-hidden="true">⌘K</kbd>
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => openDialog({ name: '', modelType: defaultModelType, startsFrom: null, title: 'New proposition' })}
            >
              <IconNew size={14} />New proposition
            </button>
          </div>
        </section>
      )}

      {palette === 'open' && (
        <OpenPalette
          rules={rules}
          propositions={entries}
          open={openIds}
          onChoose={openTab}
          onManage={() => setPalette('explorer')}
          onClose={() => setPalette(null)}
        />
      )}
      {palette === 'explorer' && (
        <PropositionExplorer
          entries={entries}
          selected={activeDoc?.kind === 'proposition' ? activeDoc.name : null}
          actions={{
            onSelect: (name) => { if (name !== null) openTab('proposition', name); },
            onClose: () => setPalette(null),
            onDerive: (name) => openDialog({
              // Prefilled to the source's namespace, so a derivation lands beside its origin.
              name: namespacePrefixOf(name), modelType: modelTypeOf(name), startsFrom: name, title: `Derive from ${name}`,
            }),
            onOverride: (name) => openDialog({
              // An override is authored under the compiled spec's *own* name, and composed from one
              // of the model's *other* specs: referencing the name being defined would be a cycle.
              name, modelType: modelTypeOf(name), startsFrom: null, title: `Override ${name}`,
            }),
            onNew: () => openDialog({ name: '', modelType: defaultModelType, startsFrom: null, title: 'New proposition' }),
            onDelete: (entry) => void removeEntry(entry),
          }}
        />
      )}
      {dialog && (
        <PropositionDialog
          // Keyed so that replacing the seed remounts rather than reuses: the dialog seeds its
          // fields from the seed once and never resyncs.
          key={dialog.title}
          seed={dialog}
          sources={entries}
          error={dialogError}
          onCancel={() => { setDialog(null); setDialogError(null); }}
          onCreate={(values) => void createFromDialog(values)}
        />
      )}
      {closingDoc && (
        <CloseQuestion
          key={closingDoc.id}
          doc={closingDoc}
          onKeep={() => setClosing(null)}
          onClose={() => closeTab(closingDoc.id)}
          // The tab's own save, and the close only once it landed — as the split button does it.
          onSaveAndClose={() => void saveAndClose(closingDoc.id)}
        />
      )}
    </>
  );
}
