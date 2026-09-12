import {
  RuleEditorStore,
  isBinaryNode, isHigherOrderNode, isNotNode, isSpecNode,
  type PropositionListEntry, type PropositionOrigin, type RuleDocument, type RuleListEntry, type RuleNode,
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

/**
 * What the workspace knows about a name: its latest version, when a tab here last saved it, and
 * the listing's description of it — what the strip's hover card says about a tab.
 */
export interface Latest {
  kind: DocKind;
  version: number;
  savedAt: number | null;
  modelType: string | null;
  isAsync: boolean;
  origin: PropositionOrigin | null;
}

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
 * Nothing here talks to the server. Each tab's own workflow loads its document into the tab's
 * store and saves it back, exactly as the pages did; the tab then tells the workspace the version
 * the save produced (`noteSaved`), which is how every other tab learns that a document it
 * references has moved, and the shell hands the listings over (`setListings`) for the rest.
 */
export class Workspace {
  #state: WorkspaceState = { tabs: [], active: null, docs: {}, latest: {} };
  readonly #listeners = new Set<() => void>();

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
  restore(): void {
    for (const tab of readRemembered()) this.open(tab.kind, tab.name, { activate: false });
  }

  /**
   * Opens a document in a new tab, or activates the tab it is already open in, and returns it.
   * The store starts on the builder's seed; the tab's workflow loads the real document into it.
   */
  open(kind: DocKind, name: string, options: { activate?: boolean } = {}): OpenDoc {
    const activate = options.activate ?? true;
    const id = tabIdOf(kind, name);
    const existing = this.#state.docs[id];
    if (existing) {
      if (activate) this.activate(id);
      return existing;
    }

    const doc: OpenDoc = {
      id, kind, name,
      store: new RuleEditorStore(PLACEHOLDER),
      seenAt: Date.now(),
    };
    this.#set({
      docs: { ...this.#state.docs, [id]: doc },
      tabs: [...this.#state.tabs, id],
      active: activate ? id : this.#state.active,
    });
    this.#remember();
    return doc;
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
    const known = this.#state.latest[name];
    this.#set({
      latest: {
        ...this.#state.latest,
        [name]: {
          kind, version, savedAt,
          modelType: known?.modelType ?? null,
          isAsync: known?.isAsync ?? false,
          origin: known?.origin ?? null,
        },
      },
      docs: doc ? { ...this.#state.docs, [id]: { ...doc, seenAt: savedAt } } : this.#state.docs,
    });
  }

  /**
   * The listings' word on every name: version and description. A version a save recorded here is
   * kept over an older listing's — the listing may be the one fetched before that save landed.
   */
  setListings(rules: readonly RuleListEntry[], propositions: readonly PropositionListEntry[]): void {
    const latest = { ...this.#state.latest };
    const take = (kind: DocKind, name: string, version: number, modelType: string, isAsync: boolean, origin: PropositionOrigin | null): void => {
      const known = latest[name];
      const savedAt = known?.savedAt ?? null;
      const kept = known !== undefined && savedAt !== null && known.version > version ? known.version : version;
      latest[name] = { kind, version: kept, savedAt, modelType, isAsync, origin };
    };
    for (const rule of rules) take('rule', rule.name, rule.version, rule.modelType, rule.isAsync, null);
    for (const prop of propositions) take('proposition', prop.name, prop.version, prop.modelType, prop.isAsync, prop.origin);
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
