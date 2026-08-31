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
  /** The unexpected failure to report against the loaded rule, or `null` when there is none. */
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
    // A failure is a claim about the load before this one, so it clears when the next one starts
    // rather than when it lands: a banner that outlives the retry it triggered reads as the retry
    // having failed too.
    if (this.#failure !== null) {
      this.#failure = null;
      this.#notify();
    }
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
    this.#saving = true;
    this.#notify();
    try {
      const result = await this.#client.putRule(
        loaded.name, this.#store.getState().document, loaded.version,
      );
      if (op !== this.#loadOp) return;
      // A typed outcome is a round trip that completed: whatever unexpected failure preceded it
      // is over, and the outcome's own channel — `conflict`, or the store's error list — carries
      // what it has to say.
      this.#failure = null;
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
}
