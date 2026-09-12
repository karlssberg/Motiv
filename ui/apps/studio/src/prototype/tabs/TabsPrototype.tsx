/*
 * PROTOTYPE — throwaway. Do not ship.
 *
 * Three variants of a multi-document tab shell, switchable via `?variant=`, mounted on Studio's
 * root route in place of the Rules / Propositions pages. Real reads, stubbed saves (see
 * workspace.ts). Everything a variant does *not* decide — opening, closing with the unsaved-changes
 * question, saving, the JSON modal, ⌘K / ⌘W — lives here so the variants differ only in layout.
 */
import { useCallback, useEffect, useMemo, useState, useSyncExternalStore, type ReactNode } from 'react';
import type { RulesApiClient } from '@motiv-rules/core';
import { RuleEditorProvider, useRuleEditor } from '@motiv-rules/react';
import { DocumentActions } from '../../shell/DocumentActions.js';
import { DiscardDialog } from '../../shell/DiscardDialog.js';
import { Toolbar } from '../../shell/Toolbar.js';
import { SplitButton, type SplitVariant } from '../../shell/SplitButton.js';
import { IconJson, IconSave } from '../../shell/icons.js';
import { useCommandKey } from '../../shell/useCommandKey.js';
import { DocumentModal } from '../../panes/DocumentModal.js';
import { OpenPalette } from './OpenPalette.js';
import { PrototypeSwitcher } from './PrototypeSwitcher.js';
import { TabDocument } from './TabDocument.js';
import { Workspace, type DocKind, type OpenDoc, type TabId, type WorkspaceState } from './workspace.js';
import { VariantA } from './VariantA.js';
import { VariantB } from './VariantB.js';
import { VariantC } from './VariantC.js';
import './prototype.css';

/** What every variant is handed. It decides layout; the host decides behaviour. */
export interface TabShellProps {
  workspace: Workspace;
  state: WorkspaceState;
  /** The active tab's document body, or the empty state. */
  body: ReactNode;
  /** The active tab's toolbar (Open / JSON / Close / Save) — the existing DocumentActions. */
  actions: ReactNode;
  /**
   * Chosen direction (C): the document's own actions (JSON, Save) and the body rendered with or
   * without them in the editor's header. The strip decides where they go — in the editor's header
   * while there are chips, in the app bar's free space once the strip is a dropdown.
   */
  docActions: ReactNode;
  bodyWith: (actionsInPanel: boolean) => ReactNode;
  onActivate: (id: TabId) => void;
  /** Close as the toolbar's Close does it: asks first when the tab has unsaved changes. */
  onRequestClose: (id: TabId) => void;
  onOpenPalette: () => void;
}

const VARIANTS = [
  { key: 'A', name: 'Browser strip — elastic tabs under the bar' },
  { key: 'B', name: 'Open documents rail — vertical, grouped by kind' },
  { key: 'C', name: 'In-bar chips — fixed width, overflow menu' },
];

function readVariant(): string {
  return new URLSearchParams(window.location.search).get('variant')?.toUpperCase() ?? 'A';
}

function writeVariant(key: string): void {
  const url = new URL(window.location.href);
  url.searchParams.set('variant', key);
  window.history.replaceState(null, '', url);
}

/** The real toolbar, bound to the active tab's store so `dirty` is that tab's. */
function ActiveActions(props: {
  doc: OpenDoc; workspace: Workspace;
  onOpen: () => void; onJson: () => void; onClose: () => void;
}) {
  const { dirty } = useRuleEditor(props.doc.store);
  return (
    <DocumentActions
      kind={props.doc.kind}
      name={props.doc.name}
      dirty={dirty}
      saveUnavailable={props.doc.status === 'ready' ? undefined : 'Still loading.'}
      onOpen={props.onOpen}
      onJson={props.onJson}
      onSave={async () => props.workspace.save(props.doc.id)}
      onClose={props.onClose}
      onDiscard={() => props.doc.store.revert()}
    />
  );
}

/**
 * Variant C's actions (the chosen direction): JSON and the split Save only, drawn in the tab's
 * panel rather than the app bar. Open is the strip's "+" / ⌘K and Close is the chip's ×, so
 * neither needs a second button here.
 */
const SAVE_VARIANTS: readonly SplitVariant[] = [
  { id: 'save', label: 'Save', description: 'Keep editing after saving.' },
  { id: 'save-close', label: 'Save & close', description: 'Save, then close this tab.' },
];

function DocActions(props: { doc: OpenDoc; workspace: Workspace; onJson: () => void }) {
  const [saveDefault, setSaveDefault] = useState('save');
  return (
    <Toolbar actions={[{ id: 'json', label: 'JSON', icon: IconJson, onActivate: props.onJson }]}>
      <SplitButton
        id="save"
        icon={IconSave}
        variants={SAVE_VARIANTS}
        defaultId={saveDefault}
        unavailable={props.doc.status === 'ready' ? undefined : 'Still loading.'}
        onChoose={(variant) => {
          setSaveDefault(variant.id);
          if (props.workspace.save(props.doc.id) && variant.id === 'save-close') props.workspace.close(props.doc.id);
        }}
      />
    </Toolbar>
  );
}

/** The unsaved-changes question, asked of a tab's own store. */
function CloseQuestion(props: { doc: OpenDoc; workspace: Workspace; onDone: () => void }) {
  const { dirty } = useRuleEditor(props.doc.store);
  useEffect(() => { if (!dirty) { props.workspace.close(props.doc.id); props.onDone(); } });
  if (!dirty) return null;
  return (
    <DiscardDialog
      name={props.doc.name}
      onKeep={props.onDone}
      onSaveAndClose={() => { if (props.workspace.save(props.doc.id)) props.workspace.close(props.doc.id); props.onDone(); }}
      onDiscard={() => { props.doc.store.revert(); props.workspace.close(props.doc.id); props.onDone(); }}
    />
  );
}

export function TabsPrototype(props: { client: RulesApiClient }) {
  const workspace = useMemo(() => new Workspace(props.client), [props.client]);
  const state = useSyncExternalStore(workspace.subscribe, workspace.getState);
  const [variant, setVariant] = useState(readVariant);
  const [picking, setPicking] = useState(false);
  const [jsonFor, setJsonFor] = useState<TabId | null>(null);
  const [closing, setClosing] = useState<TabId | null>(null);

  useEffect(() => { void workspace.refresh(); }, [workspace]);

  // Seed a realistic session once the listing lands: one rule and the propositions it uses.
  useEffect(() => {
    if (state.listingStatus !== 'ready' || state.tabs.length > 0) return;
    const rule = state.rules[0];
    if (rule) void workspace.open('rule', rule.name);
    for (const prop of state.propositions.slice(0, 3)) void workspace.open('proposition', prop.name);
    if (rule) workspace.activate(`rule:${rule.name}`);
  }, [state.listingStatus]); // eslint-disable-line react-hooks/exhaustive-deps

  useCommandKey(() => setPicking(true));
  useEffect(() => {
    const onKey = (event: KeyboardEvent): void => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'w' && state.active) {
        event.preventDefault();
        setClosing(state.active);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [state.active]);

  const open = useCallback((kind: DocKind, name: string) => { void workspace.open(kind, name); }, [workspace]);
  const active = state.active ? state.docs[state.active] ?? null : null;

  // The chosen direction (C) keeps the document's actions in its panel; A and B keep them in the bar.
  const inPanel = variant === 'C';
  const docActions = active && <DocActions doc={active} workspace={workspace} onJson={() => setJsonFor(active.id)} />;
  const bodyWith = (actionsInPanel: boolean): ReactNode => active
    ? (
      <TabDocument
        key={active.id}
        workspace={workspace}
        client={props.client}
        doc={active}
        onOpenProposition={(name) => open('proposition', name)}
        actions={actionsInPanel ? docActions : undefined}
      />
    )
    : (
      <section className="empty-state" aria-label="Nothing open">
        <h2>Nothing open</h2>
        <p>Open a rule or a proposition in a tab. Several can be open at once; each keeps its own draft.</p>
        <div className="run-row">
          <button type="button" className="btn" onClick={() => setPicking(true)}>Open<kbd aria-hidden="true">⌘K</kbd></button>
        </div>
      </section>
    );

  const actions = active && !inPanel && (
    <ActiveActions
      key={active.id}
      doc={active}
      workspace={workspace}
      onOpen={() => setPicking(true)}
      onJson={() => setJsonFor(active.id)}
      onClose={() => workspace.close(active.id)}
    />
  );

  const body = bodyWith(inPanel);
  const shell: TabShellProps = {
    workspace, state, body, actions, docActions, bodyWith,
    onActivate: (id) => workspace.activate(id),
    onRequestClose: (id) => setClosing(id),
    onOpenPalette: () => setPicking(true),
  };

  const jsonDoc = jsonFor ? state.docs[jsonFor] : undefined;
  const closingDoc = closing ? state.docs[closing] : undefined;

  return (
    <>
      {variant === 'B' ? <VariantB {...shell} /> : variant === 'C' ? <VariantC {...shell} /> : <VariantA {...shell} />}

      {picking && <OpenPalette workspace={workspace} onChoose={open} onClose={() => setPicking(false)} />}
      {jsonDoc && (
        <RuleEditorProvider store={jsonDoc.store}>
          <DocumentModal onClose={() => setJsonFor(null)} />
        </RuleEditorProvider>
      )}
      {closingDoc && <CloseQuestion key={closingDoc.id} doc={closingDoc} workspace={workspace} onDone={() => setClosing(null)} />}

      {import.meta.env.DEV && (
        <PrototypeSwitcher variants={VARIANTS} current={variant} onChange={(key) => { writeVariant(key); setVariant(key); }} />
      )}
    </>
  );
}
