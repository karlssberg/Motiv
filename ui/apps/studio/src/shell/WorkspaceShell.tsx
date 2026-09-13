import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react';
import { RuleEditorStore, type PropositionListEntry, type RuleListEntry, type RulesApiClient } from '@motiv-rules/core';
import { useRuleEditor } from '@motiv-rules/react';
import { usePropositionWorkflow } from '@motiv-rules/react/workflow';
import type { Route } from '../routing/useHashRoute.js';
import { AppBar } from '../panes/AppBar.js';
import { RuleDocument } from '../panes/RuleDocument.js';
import { PropositionDocument } from '../panes/PropositionDocument.js';
import type { SaverSink } from '../panes/documentTab.js';
import { PropositionExplorer } from '../explorer/PropositionExplorer.js';
import { PropositionDialog, type DialogSeed, type DialogValues } from '../explorer/PropositionDialog.js';
import { catalogSeedFor, promotionSeedFor } from './catalogSeeds.js';
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
 * ⌘W asks to close the active tab — the same question the chip's × asks, so a dirty tab is not
 * closed behind the person's back. Unlike ⌘K, it is not gated on whether a modal is showing.
 */
function useCloseTabKey(active: TabId | null, onRequestClose: (id: TabId) => void): void {
  useEffect(() => {
    const onKey = (event: KeyboardEvent): void => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'w' && active !== null) {
        event.preventDefault();
        onRequestClose(active);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [active, onRequestClose]);
}

/**
 * One open document, in a panel that stays mounted while its tab is hidden — which is what lets a
 * draft, an undo stack and a surface choice survive a switch.
 */
function TabPanel(props: {
  client: RulesApiClient;
  workspace: Workspace;
  doc: OpenDoc;
  active: boolean;
  actionsHost: HTMLElement | null;
  onClose: () => void;
  onOpenProposition: (name: string) => void;
  onSaver: SaverSink;
  /** The two widening flows, already bound to this document — see the shell's handlers (#234). */
  onExtractToCatalog: (path: string) => void;
  onPromote: (name: string) => void;
}) {
  const { client, workspace, doc, actionsHost, onClose, onSaver } = props;
  const common = {
    client, workspace, tab: doc, actionsHost, onClose, onSaver,
    onExtractToCatalog: props.onExtractToCatalog, onPromote: props.onPromote,
  };
  return (
    <section className="tab-panel" role="tabpanel" aria-label={doc.name} hidden={!props.active}>
      {doc.kind === 'rule'
        ? <RuleDocument {...common} onOpenProposition={props.onOpenProposition} />
        : <PropositionDocument {...common} />}
    </section>
  );
}

/** What the shell shows with no tab in front: the two ways to get one. */
function EmptyState(props: { onOpen: () => void; onNew: () => void }) {
  return (
    <section className="empty-state" aria-label="Nothing open">
      <h2>Nothing open</h2>
      <p>Open a rule or a proposition in a tab. Several can be open at once, and each keeps its own draft.</p>
      <div className="run-row">
        <button type="button" className="btn" onClick={props.onOpen}>
          <IconOpen size={14} />Open a rule or proposition<kbd aria-hidden="true">⌘K</kbd>
        </button>
        <button type="button" className="btn btn-secondary" onClick={props.onNew}>
          <IconNew size={14} />New proposition
        </button>
      </div>
    </section>
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

  // Opened in the workspace *now* and then navigated to: `hashchange` is asynchronous, so a chip
  // click that only navigated would leave the previous panel on screen for a frame, and a
  // query made in that frame would land on it. The route effect above then finds the tab already
  // open and active, and does nothing — the route is still the one writer of *which* tab, this
  // merely arrives a frame early.
  const openTab = useCallback((kind: DocKind, name: string): void => {
    workspace.open(kind, name);
    navigate({ page: PAGE_OF[kind], name });
  }, [workspace, navigate]);

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
  useEffect(() => { void refreshEntries(); void refreshRules(); }, [refreshEntries, refreshRules, state.revision]);
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
    workspace.noteChanged();
  };

  // --- palettes, dialogs, the close question ----------------------------------------------------
  const [palette, setPalette] = useState<'open' | 'explorer' | null>(null);
  const [dialog, setDialog] = useState<DialogSeed | null>(null);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [closing, setClosing] = useState<TabId | null>(null);
  const [compact, setCompact] = useState(false);
  const [actionsHost, setActionsHost] = useState<HTMLElement | null>(null);

  useCommandKey(() => setPalette('open'));
  useCloseTabKey(state.active, setClosing);

  const defaultModelType = entries.map((entry) => entry.modelType).sort()[0] ?? 'customer';
  const modelTypeOf = (name: string): string =>
    entries.find((candidate) => candidate.name === name)?.modelType ?? defaultModelType;

  /** What New authors from, wherever it is reached — the empty state, or the explorer. */
  const newPropositionSeed: DialogSeed = { name: '', modelType: defaultModelType, startsFrom: null, title: 'New proposition' };

  /**
   * What a create that lands should do to the document it came out of — rewrite the extracted node
   * as a reference, or the promoted definition's call sites. Held in a ref, and bound to the
   * *originating* tab's store when the dialog is opened: a create hands the selection to the new
   * proposition, which opens a tab of its own, so by the time this runs the active tab is no
   * longer the one being edited (#234).
   */
  const afterCreate = useRef<((createdName: string) => void) | null>(null);

  /** Opens New / Derive / Override / Extract / Promote, dismissing the palette they were reached from. */
  const openDialog = (seed: DialogSeed, onCreated: ((createdName: string) => void) | null = null): void => {
    setPalette(null);
    setDialogError(null);
    afterCreate.current = onCreated;
    setDialog(seed);
  };

  const closeDialog = (): void => {
    afterCreate.current = null;
    setDialog(null);
    setDialogError(null);
  };

  const createFromDialog = async ({ startsFrom, ...values }: DialogValues): Promise<void> => {
    // The seeded flows bring the whole document; the rest compose one from the source picked.
    const document = dialog?.document ?? (startsFrom === null ? null : { rule: { spec: startsFrom } });
    if (document === null) return;
    const rewrite = afterCreate.current;
    const refused = await create({ ...values, document });
    if (refused !== null) {
      // Reported in the dialog rather than the banner: the form still holds the input that failed.
      // The document is left exactly as it was — the rewrite is the create's second half.
      setDialogError(refused);
      return;
    }
    rewrite?.(values.name);
    closeDialog();
    workspace.noteChanged();
  };

  /**
   * *Extract to catalog*: the subtree at `path` becomes a proposition of its own, and the row it
   * stood at becomes a reference to it. Seeded with an empty name — a catalog name is namespaced,
   * and a node's own name rarely is.
   */
  const extractToCatalog = (doc: OpenDoc, path: string): void => {
    const seed = catalogSeedFor(doc.store.getState().document, path);
    if (seed === null) return;
    openDialog(
      { name: '', modelType: modelTypeOf(doc.name), startsFrom: null, title: 'Extract to catalog', document: seed },
      (createdName) => doc.store.replaceNode(path, { spec: createdName }),
    );
  };

  /**
   * *Promote to catalog*: a definition becomes a proposition, and every `local` reference to it a
   * reference to that name. The local's name is prefilled for the namespace to be typed in front
   * of it — a local name that cannot be a catalog name (one starting with `_`) is the server's to
   * refuse, and the dialog reports it.
   */
  const promoteDefinition = (doc: OpenDoc, name: string): void => {
    const seed = promotionSeedFor(doc.store.getState().document, name);
    if (seed === null) return;
    openDialog(
      { name, modelType: modelTypeOf(doc.name), startsFrom: null, title: 'Promote to catalog', document: seed },
      (createdName) => doc.store.replaceLocalWithSpec(name, createdName),
    );
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

  // Each tab's save, registered by its document: what the close question's Save & close runs. The
  // sinks are cached per tab so a document is handed the same one on every render — a fresh one
  // would make it withdraw and re-register its save each time.
  const savers = useRef<Record<TabId, () => Promise<boolean>>>({});
  const saverFor = useMemo(() => {
    const sinks: Record<TabId, SaverSink> = {};
    return (id: TabId): SaverSink => (sinks[id] ??= (save) => {
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
          <TabPanel
            key={id}
            client={client}
            workspace={workspace}
            doc={doc}
            active={active}
            // The actions move into the bar only for the tab in front, and only while the strip
            // is a dropdown; every other panel keeps them in its own editor header.
            actionsHost={active && compact ? actionsHost : null}
            onClose={() => closeTab(id)}
            onOpenProposition={(name) => openTab('proposition', name)}
            onSaver={saverFor(id)}
            onExtractToCatalog={(path) => extractToCatalog(doc, path)}
            onPromote={(name) => promoteDefinition(doc, name)}
          />
        );
      })}

      {activeDoc === null && (
        <EmptyState onOpen={() => setPalette('open')} onNew={() => openDialog(newPropositionSeed)} />
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
            onNew: () => openDialog(newPropositionSeed),
            onDelete: (entry) => void removeEntry(entry),
          }}
        />
      )}
      {dialog && (
        <PropositionDialog
          // Keyed so that replacing the seed remounts rather than reuses: the dialog seeds its
          // fields from the seed once and never resyncs.
          // The name is part of the key because two openings of the *same* flow — Promote, on one
          // definition and then another — differ only in what they seed the field with.
          key={`${dialog.title}:${dialog.name}`}
          seed={dialog}
          sources={entries}
          error={dialogError}
          onCancel={closeDialog}
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
