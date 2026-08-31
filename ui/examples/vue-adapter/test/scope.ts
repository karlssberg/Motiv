import { effectScope } from 'vue';

/**
 * Runs a composable in an effect scope and hands back a stop — the smallest thing that stands in
 * for a component's lifetime.
 *
 * A component is not needed for any of these bindings: `watch` and `onScopeDispose` are owned by
 * the scope, not by an instance, which is exactly why the adapter needs no test-utils package and
 * why a consumer can call these composables from a plain `effectScope` as readily as from
 * `setup()`. The one exception is {@link JustificationTree}, which is a component and is mounted
 * as one.
 */
export function inScope<T>(run: () => T): { value: T; stop: () => void } {
  const scope = effectScope();
  const value = scope.run(run) as T;
  return { value, stop: () => scope.stop() };
}
