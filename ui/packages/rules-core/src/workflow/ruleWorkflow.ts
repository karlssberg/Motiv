import type { RulesApiClient } from '../client.js';
import type { RuleGetResponse, RuleListEntry } from '../contracts.js';
import type { RuleEditorStore } from '../editor.js';
import { describeUnexpectedFailure } from './failureText.js';

/** The loaded rule's server identity: what a save must send back to avoid clobbering. */
export interface LoadedRule {
  name: string;
  version: number;
  /** True while the name is served by its compiled default — there is no stored document yet. */
  isCodeDefault: boolean;
}

/** The observable state of a {@link RuleWorkflowController}. */
export interface RuleWorkflowState {
  /** The live-rule listing, for whatever picker the consumer renders. */
  rules: RuleListEntry[];
  /** The picked rule's identity, or `null` while the document is a local draft. */
  loaded: LoadedRule | null;
  /** The loaded rule's listing entry, so the consumer can adapt (e.g. async validation). */
  loadedEntry: RuleListEntry | null;
  /** The version somebody else saved, when the last save hit one — the 409 to recover from. */
  conflict: number | null;
  /**
   * The last unexpected failure — whichever of refresh, load or save raised it — or `null` when
   * there is nothing to report. One channel, so the newest report owns it: every operation clears
   * it before it starts, and only a failure of the operation that is *now* running survives.
   */
  failure: string | null;
  /** True while a save is in flight. */
  saving: boolean;
}

/** Why a rule save cannot run, or `undefined` when it can. */
export function whyRuleSaveUnavailable(
  state: Pick<RuleWorkflowState, 'loaded' | 'saving'>,
): string | undefined {
  if (state.loaded === null) return 'Nothing loaded yet.';
  if (state.saving) return 'Saving…';
  return undefined;
}

/**
 * The rules-page save loop: pick a live server rule, load its document into the shared editor
 * store, and save it back optimistically with the version it was loaded at — a stale version
 * comes back as a typed conflict for the consumer to render, and recovery is loading again.
 *
 * Two channels, because a 409 and a 500 are not the same event: `conflict` carries the typed
 * refusal the API models, and `failure` carries everything it cannot see — a 500, a 404, a body
 * that will not parse. Nothing here throws to its caller; a `void save()` that reported nowhere
 * would be indistinguishable from the request never having been made.
 *
 * `failure` is one channel across all three operations rather than one per operation, and every
 * operation clears it on the way out. So a report never outlives the act that answered it —
 * neither a retry of the same operation nor a different one that has since succeeded — and the
 * consumer renders one banner rather than deciding which of three is the current truth.
 *
 * Framework-free by design: the same `subscribe`/`getState` shape as `RuleEditorStore`, adapted
 * by a UI binding (e.g. `@motiv-rules/react/workflow`'s `useRuleWorkflow`). Rendering — the
 * picker, the conflict banner, the disabled save control — stays the consumer's; this owns the
 * state and the outcome handling.
 */
export class RuleWorkflowController {
  readonly #client: RulesApiClient;
  readonly #store: RuleEditorStore;
  readonly #listeners = new Set<() => void>();

  #rules: RuleListEntry[] = [];
  #loaded: LoadedRule | null = null;
  #loadedEntry: RuleListEntry | null = null;
  #conflict: number | null = null;
  #failure: string | null = null;
  #saving = false;

  /** The state object handed out until something changes, so unchanged reads stay identical. */
  #snapshot: RuleWorkflowState | null = null;
  /** Bumped per refresh/load, so only the newest operation's answer lands. */
  #refreshOp = 0;
  #loadOp = 0;

  constructor(client: RulesApiClient, store: RuleEditorStore) {
    this.#client = client;
    this.#store = store;
  }

  getState(): RuleWorkflowState {
    this.#snapshot ??= {
      rules: this.#rules,
      loaded: this.#loaded,
      loadedEntry: this.#loadedEntry,
      conflict: this.#conflict,
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
   * Refetches the listing, reporting a failed fetch in the failure channel. A refresh superseded
   * by a newer one never lands — its answer, failure included, describes a world the newer one
   * has already replaced.
   */
  async refresh(): Promise<void> {
    const op = ++this.#refreshOp;
    this.#clearFailure();
    let rules: RuleListEntry[];
    try {
      rules = await this.#client.listRules();
    } catch (error: unknown) {
      if (op !== this.#refreshOp) return;
      this.#failure = describeUnexpectedFailure(error);
      this.#notify();
      return;
    }
    if (op !== this.#refreshOp) return;
    this.#rules = rules;
    // The loaded entry is a claim about the listing, so it follows the listing: a rule loaded
    // before the listing arrived (or renamed out of a later one) would otherwise keep a stale
    // `null`/entry while `rules` moved on.
    const loaded = this.#loaded;
    if (loaded) this.#loadedEntry = rules.find((rule) => rule.name === loaded.name) ?? null;
    this.#notify();
  }

  /**
   * Loads a rule's document into the store and takes its identity for later saves; `null` drops
   * the identity and keeps the document — it is a local draft again, with nothing to save back
   * to. Loading also clears any conflict: it *is* the 409 recovery. A load superseded by a newer
   * one never lands.
   */
  async load(name: string | null): Promise<void> {
    const op = ++this.#loadOp;
    this.#clearFailure();
    if (name === null) {
      this.#loaded = null;
      this.#loadedEntry = null;
      this.#conflict = null;
      this.#notify();
      return;
    }
    let response: RuleGetResponse;
    try {
      response = await this.#client.getRule(name);
    } catch (error: unknown) {
      if (op !== this.#loadOp) return;
      this.#failure = describeUnexpectedFailure(error);
      // The identity is left standing, unlike the proposition controller's: `loaded` is only ever
      // written *after* a load lands, so what is still there is the rule whose document is in the
      // store. Dropping it would demote a loaded rule to a local draft over a transient 500.
      this.#notify();
      return;
    }
    if (op !== this.#loadOp) return;
    this.#conflict = null;
    this.#loaded = { name, version: response.version, isCodeDefault: response.document === null };
    this.#loadedEntry = this.#rules.find((rule) => rule.name === name) ?? null;
    if (response.document) this.#store.loadDocument(response.document);
    this.#notify();
  }

  /**
   * Saves the store's current document back under the loaded identity. `updated` adopts the new
   * version (and clears `isCodeDefault` — the save is what authors the stored document);
   * `conflict` records the version somebody else saved; `invalid` routes its errors into the
   * shared store's error list, where live validation also reports.
   */
  async save(): Promise<void> {
    const loaded = this.#loaded;
    if (!loaded) return;
    // One save at a time, so `saving` cannot lie: a second PUT issued while the first is in
    // flight would have the earlier completion clear the flag under the one still running, and
    // `whyRuleSaveUnavailable` would report a save is available while one is in progress.
    if (this.#saving) return;
    // The outcome below is a claim about this identity. If a load lands while the PUT is in
    // flight, applying it would drag the state back to the previously saved rule — a version
    // badge or conflict describing something no longer on screen.
    const op = this.#loadOp;
    this.#clearFailure();
    this.#saving = true;
    this.#notify();
    try {
      const result = await this.#client.putRule(
        loaded.name, this.#store.getState().document, loaded.version,
      );
      if (op !== this.#loadOp) return;
      // Nothing to clear here: the failure went when this save started. A typed outcome reports
      // through its own channel — `conflict`, or the store's error list — and writing it into
      // `failure` as well would put one event in two banners saying different things.
      if (result.outcome === 'updated') {
        this.#conflict = null;
        this.#loaded = { name: loaded.name, version: result.version, isCodeDefault: false };
      } else if (result.outcome === 'conflict') {
        this.#conflict = result.currentVersion;
      } else {
        // Flavour-specific rejections (e.g. PolicyRequired) are invisible to live
        // validation — surface them in the shared error list.
        this.#store.setErrors(result.errors);
      }
    } catch (error: unknown) {
      // Reported rather than rethrown, and only while the load it was aimed at is still current:
      // a banner about a rule no longer on screen is false of the one that is.
      if (op === this.#loadOp) this.#failure = describeUnexpectedFailure(error);
    } finally {
      this.#saving = false;
      this.#notify();
    }
  }

  #notify(): void {
    this.#snapshot = null;
    for (const listener of this.#listeners) listener();
  }

  /**
   * Drops a standing failure because a new operation is starting. On the way out rather than on
   * the way back: a banner that outlives the act it triggered reads as that act having failed too.
   * Silent when there is nothing to drop, so an operation that reports nothing notifies nothing.
   */
  #clearFailure(): void {
    if (this.#failure === null) return;
    this.#failure = null;
    this.#notify();
  }
}
