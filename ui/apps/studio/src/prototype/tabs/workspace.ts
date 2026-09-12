/*
 * PROTOTYPE — throwaway. Do not ship.
 *
 * The in-memory workspace the tab variants share: which documents are open, which is active, and
 * a stub of "the server" that knows each name's latest version. Reads are real (the listing and
 * each document come from the API); *saves are stubbed* — they bump the version here and mark the
 * tab's store clean, without a PUT. That is enough to answer the question the prototype exists
 * for: when a proposition is saved in one tab, what does a rule tab that uses it see, and when?
 *
 * One `RuleEditorStore` per open document, rather than the app's single shared store: a tab that
 * keeps its draft while another tab is being edited is the whole point of tabs.
 */
import {
  RuleEditorStore,
  isBinaryNode, isHigherOrderNode, isNotNode, isSpecNode,
  type PropositionListEntry, type RuleDocument, type RuleListEntry, type RuleNode, type RulesApiClient,
} from '@motiv-rules/core';

export type DocKind = 'rule' | 'proposition';
export type TabId = string;

export const tabIdOf = (kind: DocKind, name: string): TabId => `${kind}:${name}`;

/** The stand-in document for a rule on its compiled default, as the real workflow leaves it. */
const PLACEHOLDER: RuleDocument = { rule: { spec: 'customer.is-active' } };

export interface OpenDoc {
  id: TabId;
  kind: DocKind;
  name: string;
  modelType: string;
  version: number;
  origin: string | null;
  isAsync: boolean;
  isCodeDefault: boolean;
  status: 'loading' | 'ready' | 'failed';
  error: string | null;
  store: RuleEditorStore;
  /** When the tab last took the server's word for its references — what "changed since" is measured from. */
  seenAt: number;
}

/** What the stub server knows about a name: its latest version, and when it last changed here. */
export interface Latest { version: number; savedAt: number | null; kind: DocKind }

export interface WorkspaceState {
  tabs: TabId[];
  active: TabId | null;
  docs: Record<TabId, OpenDoc>;
  latest: Record<string, Latest>;
  rules: RuleListEntry[];
  propositions: PropositionListEntry[];
  listingStatus: 'loading' | 'ready' | 'failed';
}

/** Every spec a document references, in document order, de-duplicated. */
export function referencesOf(document: RuleDocument): string[] {
  const found: string[] = [];
  const walk = (node: RuleNode): void => {
    if (isSpecNode(node)) { if (!found.includes(node.spec)) found.push(node.spec); return; }
    if (isNotNode(node)) { walk(node.not); return; }
    if (isBinaryNode(node)) {
      for (const child of Object.values(node)) if (Array.isArray(child)) child.forEach(walk);
      return;
    }
    if (isHigherOrderNode(node)) {
      for (const child of Object.values(node)) if (child && typeof child === 'object' && !Array.isArray(child)) walk(child as RuleNode);
    }
  };
  walk(document.rule);
  return found;
}

export class Workspace {
  #state: WorkspaceState = {
    tabs: [], active: null, docs: {}, latest: {}, rules: [], propositions: [], listingStatus: 'loading',
  };
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

  async refresh(): Promise<void> {
    try {
      const [rules, propositions] = await Promise.all([this.#client.listRules(), this.#client.listPropositions()]);
      const latest: Record<string, Latest> = { ...this.#state.latest };
      for (const rule of rules) latest[rule.name] ??= { version: rule.version, savedAt: null, kind: 'rule' };
      for (const prop of propositions) latest[prop.name] ??= { version: prop.version, savedAt: null, kind: 'proposition' };
      this.#set({ rules, propositions, latest, listingStatus: 'ready' });
    } catch {
      this.#set({ listingStatus: 'failed' });
    }
  }

  /** Opens a document in a tab, or activates the tab it is already open in. */
  async open(kind: DocKind, name: string): Promise<void> {
    const id = tabIdOf(kind, name);
    if (this.#state.docs[id]) { this.activate(id); return; }

    const entry = kind === 'rule'
      ? this.#state.rules.find((rule) => rule.name === name)
      : this.#state.propositions.find((prop) => prop.name === name);
    const doc: OpenDoc = {
      id, kind, name,
      modelType: entry?.modelType ?? 'customer',
      version: entry?.version ?? 0,
      origin: entry && 'origin' in entry ? entry.origin : null,
      isAsync: entry?.isAsync ?? false,
      isCodeDefault: false,
      status: 'loading',
      error: null,
      store: new RuleEditorStore(PLACEHOLDER),
      seenAt: Date.now(),
    };
    this.#set({ docs: { ...this.#state.docs, [id]: doc }, tabs: [...this.#state.tabs, id], active: id });

    try {
      const response = kind === 'rule' ? await this.#client.getRule(name) : await this.#client.getProposition(name);
      if (response.document) doc.store.loadDocument(response.document);
      doc.store.markClean();
      this.#patchDoc(id, { status: 'ready', version: response.version, isCodeDefault: response.document === null });
    } catch (error: unknown) {
      this.#patchDoc(id, { status: 'failed', error: error instanceof Error ? error.message : String(error) });
    }
  }

  activate(id: TabId): void {
    if (this.#state.docs[id]) this.#set({ active: id });
  }

  /** Closes a tab; the neighbour to the right (else the left) becomes active, as browsers do it. */
  close(id: TabId): void {
    const index = this.#state.tabs.indexOf(id);
    if (index < 0) return;
    const tabs = this.#state.tabs.filter((tab) => tab !== id);
    const { [id]: _closed, ...docs } = this.#state.docs;
    const active = this.#state.active === id
      ? (tabs[index] ?? tabs[index - 1] ?? null)
      : this.#state.active;
    this.#set({ tabs, docs, active });
  }

  /** Moves a tab to a new index (drag-to-reorder). */
  move(id: TabId, to: number): void {
    const tabs = this.#state.tabs.filter((tab) => tab !== id);
    tabs.splice(Math.max(0, Math.min(to, tabs.length)), 0, id);
    this.#set({ tabs });
  }

  /**
   * STUB save: adopts the draft as the new baseline and bumps the version the "server" knows.
   * Every other tab reads `latest`, so a rule tab referencing this name sees the bump at once.
   */
  save(id: TabId): boolean {
    const doc = this.#state.docs[id];
    if (!doc || doc.status !== 'ready') return false;
    const version = doc.version + 1;
    doc.store.markClean();
    const savedAt = Date.now();
    this.#set({
      docs: { ...this.#state.docs, [id]: { ...doc, version, isCodeDefault: false, seenAt: savedAt } },
      latest: { ...this.#state.latest, [doc.name]: { version, savedAt, kind: doc.kind } },
    });
    return true;
  }

  /** The tab takes the server's word for its references again: "changed since" resets. */
  acknowledge(id: TabId): void {
    this.#patchDoc(id, { seenAt: Date.now() });
  }
}

/** What a rule tab knows about one proposition it references. */
export interface ReferenceStatus {
  name: string;
  version: number | null;
  /** Saved (here, in another tab) since this tab last looked. */
  changedSinceSeen: boolean;
  /** Open in another tab with unsaved edits — a change that is coming, not one that has landed. */
  editingElsewhere: boolean;
  openIn: TabId | null;
}

export function referenceStatuses(state: WorkspaceState, doc: OpenDoc, references: string[]): ReferenceStatus[] {
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
