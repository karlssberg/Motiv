import {
  RuleEditorStore,
  isBinaryNode, isHigherOrderNode, isNotNode, isSpecNode,
  type RuleDocument, type RuleNode, type RulesApiClient,
} from '@motiv-rules/core';

/** The two kinds of document a tab can hold. */
export type DocKind = 'rule' | 'proposition';

/** A tab's identity: its kind and name, joined. */
export type TabId = string;

export const tabIdOf = (kind: DocKind, name: string): TabId => `${kind}:${name}`;

/** Where the open-tab list is remembered. Session-scoped: two browser tabs are two workspaces. */
export const TABS_KEY = 'motiv.studio.tabs';

/**
 * What the builder starts from for a document the server has not authored — a rule on its
 * compiled default, or a purely compiled proposition. The same seed the shared store used to hold.
 */
const PLACEHOLDER: RuleDocument = { rule: { spec: 'customer.is-active' } };

/** One open document. */
export interface OpenDoc {
  id: TabId;
  kind: DocKind;
  name: string;
  /** The version the tab holds — the loaded one, then whatever its last save produced. */
  version: number;
  status: 'loading' | 'ready' | 'failed';
  error: string | null;
  /**
   * This tab's own editor store. One per document rather than one shared by the shell: a tab that
   * keeps its draft, its undo stack and its dirty flag while another is edited is what tabs are.
   */
  store: RuleEditorStore;
  /**
   * When the tab last took the workspace's word for the documents it references — what "changed
   * since you looked" is measured from. Set on open, on the tab's own save, and on acknowledge.
   */
  seenAt: number;
}

/** What the workspace knows about a name: its latest version, and when a tab here last saved it. */
export interface Latest { kind: DocKind; version: number; savedAt: number | null }

export interface WorkspaceState {
  /** Open tabs, in strip order. */
  tabs: TabId[];
  active: TabId | null;
  docs: Record<TabId, OpenDoc>;
  latest: Record<string, Latest>;
}

/** A remembered tab: enough to reopen it. */
interface RememberedTab { kind: DocKind; name: string }

function readRemembered(): RememberedTab[] {
  try {
    const raw = window.sessionStorage.getItem(TABS_KEY);
    if (raw === null) return [];
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed.filter((entry): entry is RememberedTab =>
      typeof entry === 'object' && entry !== null
      && ((entry as RememberedTab).kind === 'rule' || (entry as RememberedTab).kind === 'proposition')
      && typeof (entry as RememberedTab).name === 'string');
  } catch {
    // No storage, or a value some other build wrote: nothing to restore.
    return [];
  }
}

function writeRemembered(tabs: RememberedTab[]): void {
  try {
    window.sessionStorage.setItem(TABS_KEY, JSON.stringify(tabs));
  } catch {
    // Not remembered; the tabs are still open.
  }
}

/** Every spec a document references, in document order, de-duplicated. */
export function referencesOf(document: RuleDocument): string[] {
  const found: string[] = [];
  const walk = (node: RuleNode): void => {
    if (isSpecNode(node)) {
      if (!found.includes(node.spec)) found.push(node.spec);
      return;
    }
    if (isNotNode(node)) { walk(node.not); return; }
    if (isBinaryNode(node)) {
      for (const child of Object.values(node)) if (Array.isArray(child)) child.forEach(walk);
      return;
    }
    if (isHigherOrderNode(node)) {
      for (const child of Object.values(node)) {
        if (child !== null && typeof child === 'object' && !Array.isArray(child)) walk(child as RuleNode);
      }
    }
  };
  walk(document.rule);
  return found;
}

/**
 * The open documents, one editor store each, and what the workspace knows about every name's
 * latest version. The hash route stays the truth for *which* tab is active — `App` maps a named
 * route onto `open`, and activation back onto the route — so this holds the set, not the cursor.
 *
 * Saves reach the server through each tab's own workflow, exactly as before; the tab then tells
 * the workspace the version it produced (`noteSaved`), which is how every other tab learns that a
 * document it references has moved. Nothing here talks to the server except `open`, which loads
 * the document into the tab's fresh store.
 */
export class Workspace {
  #state: WorkspaceState = { tabs: [], active: null, docs: {}, latest: {} };
  readonly #listeners = new Set<() => void>();
  readonly #client: RulesApiClient;

  constructor(client: RulesApiClient) {
    this.#client = client;
  }

  getState = (): WorkspaceState => this.#state;

  subscribe = (listener: () => void): (() => void) => {
    this.#listeners.add(listener);
    return () => this.#listeners.delete(listener);
  };

  #set(patch: Partial<WorkspaceState>): void {
    this.#state = { ...this.#state, ...patch };
    for (const listener of this.#listeners) listener();
  }

  #patchDoc(id: TabId, patch: Partial<OpenDoc>): void {
    const doc = this.#state.docs[id];
    if (!doc) return;
    this.#set({ docs: { ...this.#state.docs, [id]: { ...doc, ...patch } } });
  }

  #remember(): void {
    writeRemembered(this.#state.tabs.flatMap((id) => {
      const doc = this.#state.docs[id];
      return doc ? [{ kind: doc.kind, name: doc.name }] : [];
    }));
  }

  /** Reopens the tabs a previous page load left open. Activates none: the route says which. */
  async restore(): Promise<void> {
    const remembered = readRemembered();
    for (const tab of remembered) await this.open(tab.kind, tab.name, { activate: false });
  }

  /**
   * Opens a document in a new tab, or activates the tab it is already open in. Resolves once the
   * document has loaded (or failed to); the tab exists, loading, from the first call.
   */
  async open(kind: DocKind, name: string, options: { activate?: boolean } = {}): Promise<void> {
    const activate = options.activate ?? true;
    const id = tabIdOf(kind, name);
    if (this.#state.docs[id]) {
      if (activate) this.activate(id);
      return;
    }

    const doc: OpenDoc = {
      id, kind, name,
      version: this.#state.latest[name]?.version ?? 0,
      status: 'loading',
      error: null,
      store: new RuleEditorStore(PLACEHOLDER),
      seenAt: Date.now(),
    };
    this.#set({
      docs: { ...this.#state.docs, [id]: doc },
      tabs: [...this.#state.tabs, id],
      active: activate ? id : this.#state.active,
    });
    this.#remember();

    try {
      const response = kind === 'rule'
        ? await this.#client.getRule(name)
        : await this.#client.getProposition(name);
      if (response.document) doc.store.loadDocument(response.document);
      // What is in the store now is what the server has — for a code-defined default, the
      // placeholder is what the builder starts from, so it is the baseline too.
      doc.store.markClean();
      this.#patchDoc(id, { status: 'ready', version: response.version });
    } catch (error: unknown) {
      this.#patchDoc(id, { status: 'failed', error: error instanceof Error ? error.message : String(error) });
    }
  }

  activate(id: TabId): void {
    if (this.#state.docs[id] && this.#state.active !== id) this.#set({ active: id });
  }

  /**
   * Closes a tab. When it was the active one, the neighbour to its right — else its left — takes
   * over, which is how every browser does it. Does not ask about unsaved changes: that is the
   * shell's question, asked before it gets here.
   */
  close(id: TabId): void {
    const index = this.#state.tabs.indexOf(id);
    if (index < 0) return;
    const tabs = this.#state.tabs.filter((tab) => tab !== id);
    const { [id]: _closed, ...docs } = this.#state.docs;
    const active = this.#state.active === id
      ? (tabs[index] ?? tabs[index - 1] ?? null)
      : this.#state.active;
    this.#set({ tabs, docs, active });
    this.#remember();
  }

  /** Moves a tab to a new position in the strip. */
  move(id: TabId, to: number): void {
    if (!this.#state.docs[id]) return;
    const tabs = this.#state.tabs.filter((tab) => tab !== id);
    tabs.splice(Math.max(0, Math.min(to, tabs.length)), 0, id);
    this.#set({ tabs });
    this.#remember();
  }

  /**
   * A tab saved and the server answered with a version. The name's latest moves, so every tab
   * that references it can see the change; the saver re-baselines, since what it holds *is* the
   * latest now.
   */
  noteSaved(kind: DocKind, name: string, version: number): void {
    const savedAt = Date.now();
    const id = tabIdOf(kind, name);
    const doc = this.#state.docs[id];
    this.#set({
      latest: { ...this.#state.latest, [name]: { kind, version, savedAt } },
      docs: doc ? { ...this.#state.docs, [id]: { ...doc, version, seenAt: savedAt } } : this.#state.docs,
    });
  }

  /**
   * The listings' word on every name's version. A version a save recorded here is kept over the
   * listing's — the listing may be the one fetched before that save landed.
   */
  setLatest(entries: ReadonlyArray<{ kind: DocKind; name: string; version: number }>): void {
    const latest = { ...this.#state.latest };
    for (const entry of entries) {
      const known = latest[entry.name];
      if (known && known.savedAt !== null && known.version >= entry.version) continue;
      latest[entry.name] = { kind: entry.kind, version: entry.version, savedAt: known?.savedAt ?? null };
    }
    this.#set({ latest });
  }

  /** The tab takes the workspace's word for its references again: "changed since" resets. */
  acknowledge(id: TabId): void {
    this.#patchDoc(id, { seenAt: Date.now() });
  }
}

/** What a rule tab knows about one proposition it references. */
export interface ReferenceStatus {
  name: string;
  /** The latest version the workspace knows, or `null` for a name no listing has reported. */
  version: number | null;
  /** Saved in another tab since this tab last looked — a change that has landed. */
  changedSinceSeen: boolean;
  /** Open in another tab with unsaved edits — a change that is coming, not one that has landed. */
  editingElsewhere: boolean;
  /** The tab the referenced proposition is open in, if any. */
  openIn: TabId | null;
}

export function referenceStatuses(
  state: WorkspaceState, doc: OpenDoc, references: readonly string[],
): ReferenceStatus[] {
  return references.map((name) => {
    const latest = state.latest[name];
    const openIn = state.docs[tabIdOf('proposition', name)] ?? null;
    return {
      name,
      version: latest?.version ?? null,
      changedSinceSeen: (latest?.savedAt ?? 0) > doc.seenAt,
      editingElsewhere: openIn !== null && openIn.id !== doc.id && openIn.store.getState().dirty,
      openIn: openIn?.id ?? null,
    };
  });
}
