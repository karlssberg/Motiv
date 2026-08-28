import type { RulesApiClient } from '../client.js';
import type {
  DependentEntry, PropositionCreateRequest, PropositionListEntry, PropositionSaveResult,
} from '../contracts.js';
import type { RuleEditorStore } from '../editor.js';
import { describePropositionFailure, describeUnexpectedFailure } from './failureText.js';

/** The loaded proposition's server identity: what a save must send back to avoid clobbering. */
export interface LoadedProposition {
  name: string;
  /** 0 when the proposition is purely compiled — nothing authored exists for a save to update. */
  version: number;
}

/** The observable state of a {@link PropositionWorkflowController}. */
export interface PropositionWorkflowState {
  /** Every proposition in scope, compiled or authored. */
  entries: PropositionListEntry[];
  /** The selected proposition's identity, or `null` while nothing is selected. */
  loaded: LoadedProposition | null;
  /** The blast radius: what a save of the selected proposition would rebind. */
  dependents: DependentEntry[];
  /** The failure to report against the selection, or `null` when there is nothing to report. */
  failure: string | null;
  /** True while a save is in flight. */
  saving: boolean;
}

/** Callbacks a {@link PropositionWorkflowController} reports through. */
export interface PropositionWorkflowOptions {
  /**
   * Where the selection should go after an act that moves it: the created proposition's name, the
   * reverted proposition's own name (it survives, served by its compiled spec now), or `null`
   * after an outright delete. The consumer owns navigation — typically this feeds a route, and
   * the route drives {@link PropositionWorkflowController.select} back in.
   */
  onSelect?: (name: string | null) => void;
}

/** Why a proposition save cannot run, or `undefined` when it can. */
export function whyPropositionSaveUnavailable(
  state: Pick<PropositionWorkflowState, 'loaded' | 'saving'>,
): string | undefined {
  if (state.loaded === null) return 'Nothing loaded yet.';
  // Version 0 is the contract's "purely compiled": no overlay document exists for a PUT to
  // update, and `baseVersion` must be positive, so Save could only ever fail there. Authoring
  // one is what Override is for.
  if (state.loaded.version === 0) return 'This name is served by a compiled spec. Use Override to author one.';
  if (state.saving) return 'Saving…';
  return undefined;
}

/**
 * The propositions save loop: select a proposition (its document and its blast radius arrive
 * together), save it back optimistically with the version it was loaded at, delete-or-revert,
 * and create — with every typed refusal rendered as failure text and every unexpected throw
 * reported rather than swallowed.
 *
 * An outcome only lands while the selection it was aimed at is still current: a save, delete or
 * failed load that resolves after the selection moved on is dropped, because its claims — a
 * version badge, a conflict banner — would land on whatever is showing now and be false of it.
 *
 * Framework-free by design: the same `subscribe`/`getState` shape as `RuleEditorStore`, adapted
 * by a UI binding (e.g. `@motiv-rules/react/workflow`'s `usePropositionWorkflow`). Navigation is
 * reported through {@link PropositionWorkflowOptions.onSelect}, never performed.
 */
export class PropositionWorkflowController {
  readonly #client: RulesApiClient;
  readonly #store: RuleEditorStore;
  readonly #onSelect: ((name: string | null) => void) | undefined;
  readonly #listeners = new Set<() => void>();

  #entries: PropositionListEntry[] = [];
  #loaded: LoadedProposition | null = null;
  #dependents: DependentEntry[] = [];
  #failure: string | null = null;
  #saving = false;

  /** What the workflow is *currently* about — the guard async continuations check against. */
  #selected: string | null = null;
  /** Bumped per select, so only the newest selection's answer lands. */
  #selectOp = 0;
  /** The state object handed out until something changes, so unchanged reads stay identical. */
  #snapshot: PropositionWorkflowState | null = null;

  constructor(client: RulesApiClient, store: RuleEditorStore, options: PropositionWorkflowOptions = {}) {
    this.#client = client;
    this.#store = store;
    this.#onSelect = options.onSelect;
  }

  getState(): PropositionWorkflowState {
    this.#snapshot ??= {
      entries: this.#entries,
      loaded: this.#loaded,
      dependents: this.#dependents,
      failure: this.#failure,
      saving: this.#saving,
    };
    return this.#snapshot;
  }

  subscribe(listener: () => void): () => void {
    this.#listeners.add(listener);
    return () => this.#listeners.delete(listener);
  }

  /**
   * Refetches the listing, reporting a failed reload in the failure banner. The listing is
   * reloaded on the back of some other act, so a reload that fails has to say so on its own
   * behalf or nothing says it at all. `save` is the exception — its reload runs inside a
   * continuation that decides whether reporting is still honest, so it fetches directly and
   * lets that decision have the last word.
   */
  async refreshEntries(): Promise<void> {
    try {
      await this.#fetchEntries();
    } catch (error: unknown) {
      this.#failure = describeUnexpectedFailure(error);
      this.#notify();
    }
  }

  /**
   * Selects a proposition, fetching its document and its blast radius together; `null` clears
   * the selection. Failure and dependents are claims about the *previous* selection, so they
   * clear before the fetch, not when it lands; `loaded` is replaced wholesale on arrival, since
   * blanking the breadcrumb for one round trip buys nothing. A select superseded by a newer one
   * never lands.
   */
  async select(name: string | null): Promise<void> {
    const op = ++this.#selectOp;
    this.#selected = name;
    this.#failure = null;
    this.#dependents = [];

    if (name === null) {
      this.#loaded = null;
      this.#notify();
      return;
    }
    this.#notify();

    try {
      const [proposition, affected] = await Promise.all([
        this.#client.getProposition(name),
        this.#client.getDependents(name),
      ]);
      if (op !== this.#selectOp) return;
      this.#dependents = affected;
      this.#loaded = { name, version: proposition.version };
      if (proposition.document) this.#store.loadDocument(proposition.document);
      this.#notify();
    } catch (error: unknown) {
      if (op !== this.#selectOp) return;
      this.#failure = describeUnexpectedFailure(error);
      // `loaded` is deliberately left standing while a load is *in flight* — see above — but once
      // the load has failed there is nothing coming to replace it, and a stale identity would go
      // on naming a proposition the selection no longer points at.
      this.#loaded = null;
      this.#notify();
    }
  }

  /**
   * Refetches behind the current selection — for when the name still names something, but
   * something *different*: a revert, or recovering from a conflict. The selection cannot
   * express that, since the name did not change.
   */
  reload(): Promise<void> {
    return this.select(this.#selected);
  }

  /**
   * Saves the store's current document back under the selected identity. A success adopts the
   * new version and refetches the listing; every refusal lands as failure text. The outcome is
   * a claim about the selection the save was aimed at, and is dropped if that has moved on.
   */
  async save(): Promise<void> {
    const saved = this.#loaded;
    if (!saved) return;
    this.#saving = true;
    this.#notify();
    try {
      const result = await this.#client.putProposition(
        saved.name, this.#store.getState().document, saved.version,
      );
      if (this.#selected !== saved.name) return;
      this.#failure = describePropositionFailure(result);
      if (result.outcome === 'saved') {
        this.#loaded = { name: saved.name, version: result.version };
        this.#notify();
        await this.#fetchEntries();
      }
    } catch (error: unknown) {
      // Covers the listing refetch as well as the PUT, and deliberately: this is the one place
      // where a failed reload is reported only while the selection it followed is still current.
      if (this.#selected === saved.name) this.#failure = describeUnexpectedFailure(error);
    } finally {
      this.#saving = false;
      this.#notify();
    }
  }

  /**
   * Deletes an authored proposition — or reverts an overridden one to its compiled spec: DELETE
   * answers the same `{ version: 0 }` either way, so which is about to happen is read off the
   * entry *before* the call. A revert keeps the selection (the name survives) and refetches
   * behind it; a delete hands the selection over as `null`. An outcome for an entry that is no
   * longer selected is dropped, navigation included: acting on its behalf would drag the user
   * off whatever they moved to while it was in flight.
   */
  async remove(entry: PropositionListEntry): Promise<void> {
    const reverts = entry.origin === 'Overridden';
    let result: PropositionSaveResult;
    try {
      result = await this.#client.deleteProposition(entry.name, entry.version);
    } catch (error: unknown) {
      if (this.#selected === entry.name) {
        this.#failure = describeUnexpectedFailure(error);
        this.#notify();
      }
      return;
    }
    if (this.#selected !== entry.name) return;
    this.#failure = describePropositionFailure(result);
    this.#notify();
    if (result.outcome !== 'saved') return;
    await this.refreshEntries();

    if (reverts) {
      await this.reload();
      this.#onSelect?.(entry.name);
      return;
    }
    this.#onSelect?.(null);
  }

  /**
   * Authors a new proposition, answering the failure to show — `null` on success — rather than
   * reporting into the page banner: the consumer's form still holds the input that failed, and
   * the refusal belongs beside it. On success the listing refreshes and the selection is handed
   * over to the new name.
   */
  async create(request: PropositionCreateRequest): Promise<string | null> {
    let result: PropositionSaveResult;
    try {
      result = await this.#client.createProposition(request);
    } catch (error: unknown) {
      return describeUnexpectedFailure(error);
    }
    if (result.outcome !== 'saved') return describePropositionFailure(result);

    await this.refreshEntries();
    this.#onSelect?.(request.name);
    return null;
  }

  #notify(): void {
    this.#snapshot = null;
    for (const listener of this.#listeners) listener();
  }

  /** The listing fetch itself, throwing to the caller — who decides whether reporting is honest. */
  async #fetchEntries(): Promise<void> {
    this.#entries = await this.#client.listPropositions();
    this.#notify();
  }
}
