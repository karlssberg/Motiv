import { onScopeDispose, shallowRef, toValue, watch, type MaybeRefOrGetter, type ShallowRef } from 'vue';

/**
 * The shape every observable thing in `@motiv-rules/core` has: one snapshot, one subscription.
 * `RuleEditorStore`, `DslSyncController`, `RuleWorkflowController` and
 * `PropositionWorkflowController` all expose exactly this and nothing framework-shaped.
 */
export interface Observable<TState> {
  getState(): TState;
  subscribe(listener: () => void): () => void;
}

/** The state one of them holds. */
export type StateOf<TObject extends Observable<unknown>> = ReturnType<TObject['getState']>;

/** What {@link observe} hands back: the live state, and a way to reach the current object. */
export interface Observed<TObject extends Observable<unknown>> {
  /** The current snapshot. Replaced whenever the object notifies, or the object itself changes. */
  readonly state: ShallowRef<StateOf<TObject>>;
  /**
   * Runs `act` against whichever object is current. Actions go through this rather than closing
   * over the object, because a source change replaces it and a closure would keep driving the
   * one that was let go.
   */
  dispatch<TResult>(act: (object: TObject) => TResult): TResult;
}

/**
 * Follows one of core's `subscribe`/`getState` objects for as long as the calling scope lives,
 * rebuilding it whenever a source it was built from changes.
 *
 * **This is the whole adapter.** Every composable in this package is a call to it plus a handful
 * of typed actions, because everything an authoring UI needs to know is already computed in
 * `@motiv-rules/core` — the binding's only job is to make a subscription a scope's business and a
 * snapshot a reactive value.
 *
 * Three deliberate choices, each answering something `@motiv-rules/react` has to do differently:
 *
 * - **No snapshot caching.** The React adapter compares the fields of every snapshot and hands
 *   back the previous object when they match, because `useSyncExternalStore` tears without a
 *   referentially stable one. Vue re-runs an effect when the ref is *set*, so a fresh object per
 *   notification is exactly right — and the comparison is not merely unnecessary here, it would be
 *   wrong: the state a snapshot describes lives behind a new wrapper each time, so a binding that
 *   deduplicated on those fields would go stale rather than quiet.
 * - **`flush: 'sync'` on the rebind.** A source swap has to take effect before the consumer's next
 *   line, not on the next tick. React gets that from re-running the hook during render; this is
 *   its equivalent.
 * - **`dispatch` rather than bound actions.** React re-runs the hook every render, so the object an
 *   action closes over is always the current one. In Vue `setup` runs once, so the indirection has
 *   to be explicit — and it lives here, once, instead of in each composable.
 */
export function observe<TObject extends Observable<unknown>>(
  sources: readonly MaybeRefOrGetter<unknown>[],
  open: () => TObject,
  connect?: (object: TObject) => () => void,
): Observed<TObject> {
  let object = open();
  // `getState` is reached through the constraint here, which types its result as `unknown`.
  // `StateOf<TObject>` is what it actually is, and this is the whole of the difference.
  const read = (): StateOf<TObject> => object.getState() as StateOf<TObject>;

  const state: ShallowRef<StateOf<TObject>> = shallowRef<StateOf<TObject>>(read());

  const follow = (): (() => void) => {
    const unsubscribe = object.subscribe(() => { state.value = read(); });
    const disconnect = connect?.(object);
    return () => { unsubscribe(); disconnect?.(); };
  };

  let release = follow();

  watch(sources.map((source) => () => toValue(source)), () => {
    release();
    object = open();
    state.value = read();
    release = follow();
  }, { flush: 'sync' });

  onScopeDispose(() => release());

  return { state, dispatch: (act) => act(object) };
}
