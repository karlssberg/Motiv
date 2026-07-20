import type { RulesApiClient } from './client.js';
import type { RuleEditorStore } from './editor.js';

/** Options for the debounced validation controller. */
export interface ValidationControllerOptions {
  /** The model type id to validate against. */
  modelType: string;
  /** Idle delay before validating after the last edit. Defaults to 300ms. */
  debounceMs?: number;
}

/**
 * Subscribes to a store and, after each burst of edits settles, validates the
 * current document and writes the returned errors back onto the store. Returns a
 * dispose function that unsubscribes and cancels any pending validation.
 */
export function createValidationController(
  store: RuleEditorStore,
  client: RulesApiClient,
  options: ValidationControllerOptions,
): () => void {
  const debounceMs = options.debounceMs ?? 300;
  let timer: ReturnType<typeof setTimeout> | undefined;
  let lastDocument = store.getState().document;

  const run = (): void => {
    const { document } = store.getState();
    void client
      .validate({ modelType: options.modelType, document })
      .then((response) => store.setErrors(response.errors))
      .catch(() => { /* transport failures leave prior errors in place */ });
  };

  const unsubscribe = store.subscribe(() => {
    // Ignore notifications that are not document changes (e.g. setErrors).
    const { document } = store.getState();
    if (document === lastDocument) return;
    lastDocument = document;
    if (timer) clearTimeout(timer);
    timer = setTimeout(run, debounceMs);
  });

  return () => {
    if (timer) clearTimeout(timer);
    unsubscribe();
  };
}
