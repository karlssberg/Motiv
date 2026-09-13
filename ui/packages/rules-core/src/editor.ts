import {
  binaryOperator, isBinaryNode, isLocalNode, isNotNode, operandsOf,
  type BinaryOperator, type Decoration, type Definition, type Payload, type RuleDocument, type RuleNode,
} from './document.js';
import type { RuleError } from './contracts.js';
import { definitionBodyPath, definitionPath, getNode, localReferences, setNode, splitLast } from './paths.js';
import { isValidLocalName } from './localNames.js';

/** The observable state of a rule editor. */
export interface EditorState {
  document: RuleDocument;
  errors: RuleError[];
  canUndo: boolean;
  canRedo: boolean;
  /**
   * Whether the document differs from its baseline — what was last loaded by a workflow or
   * saved back. Compared structurally, not by reference: the DSL sync replaces the document
   * wholesale on every text commit, so an edit typed back to what was loaded is clean again.
   */
  dirty: boolean;
}

/** Structural equality over the plain JSON a rule document is made of. */
function sameDocument(a: unknown, b: unknown): boolean {
  if (a === b) return true;
  if (typeof a !== 'object' || typeof b !== 'object' || a === null || b === null) return false;
  if (Array.isArray(a) !== Array.isArray(b)) return false;
  const keysA = Object.keys(a);
  const keysB = Object.keys(b);
  if (keysA.length !== keysB.length) return false;
  return keysA.every((key) => (
    Object.prototype.hasOwnProperty.call(b, key)
    && sameDocument((a as Record<string, unknown>)[key], (b as Record<string, unknown>)[key])
  ));
}

/** Errors anchored on a node or any of its sub-field paths (e.g. whenTrue). */
export function errorsForNode(errors: RuleError[], path: string): RuleError[] {
  const prefix = `${path}.`;
  return errors.filter((error) => error.path === path || error.path.startsWith(prefix));
}

/** A synchronous, subscribable store over an immutable rule document. */
export class RuleEditorStore {
  #document: RuleDocument;
  #errors: RuleError[] = [];
  #undo: RuleDocument[] = [];
  #redo: RuleDocument[] = [];
  /**
   * The document the workflow last loaded or saved. Not moved by `loadDocument`, which the DSL
   * sync also calls to commit a text edit; only {@link markClean} moves it.
   */
  #baseline: RuleDocument;
  /** `dirty` memoised against the document it was computed for: it is read on every render. */
  #dirtyFor: RuleDocument | null = null;
  #dirty = false;
  readonly #listeners = new Set<() => void>();

  constructor(initial: RuleDocument) {
    this.#document = structuredClone(initial);
    this.#baseline = this.#document;
  }

  getState(): EditorState {
    return {
      document: this.#document,
      errors: this.#errors,
      canUndo: this.#undo.length > 0,
      canRedo: this.#redo.length > 0,
      dirty: this.#isDirty(),
    };
  }

  /**
   * Adopts the current document as the baseline `dirty` is measured from. The workflows call it
   * when a document has just been loaded or has just been saved — the two moments the store's
   * content and the server's agree.
   */
  markClean(): void {
    this.#baseline = this.#document;
    this.#dirtyFor = null;
    this.#notify();
  }

  /**
   * Throws the changes away: the document goes back to the baseline, as a fresh load would leave
   * it. "Discard" has to mean this rather than merely closing — a code-defined default fetches no
   * document on load, so an edit left in the store would resurface the next time it was opened.
   */
  revert(): void {
    this.#document = this.#baseline;
    this.#errors = [];
    this.#undo = [];
    this.#redo = [];
    this.#dirtyFor = null;
    this.#notify();
  }

  #isDirty(): boolean {
    if (this.#dirtyFor !== this.#document) {
      this.#dirty = !sameDocument(this.#document, this.#baseline);
      this.#dirtyFor = this.#document;
    }
    return this.#dirty;
  }

  subscribe(listener: () => void): () => void {
    this.#listeners.add(listener);
    return () => this.#listeners.delete(listener);
  }

  replaceNode(path: string, node: RuleNode): void {
    this.#commit(setNode(this.#document, path, node));
  }

  /**
   * Commits a document produced by the planner, as one undoable edit.
   *
   * Distinct from `loadDocument`, which installs a fresh baseline and clears history: a planned
   * insertion or move is an edit like any other and must be undoable. Distinct from `replaceNode`
   * because a plan is not addressed to a node — normalization may have rewritten a parent, or
   * collapsed one, above the point of change.
   *
   * Unlike `loadDocument`, this stores `next` **by reference**, not a `structuredClone` of it.
   * Safe today because every caller passes a document a planner function just produced and
   * retains no reference to — but it is an asymmetry worth knowing about: a caller that mutates
   * `next` after passing it here, or that reuses the same object across two calls, would corrupt
   * history entries that are supposed to be immutable snapshots.
   */
  applyPlan(next: RuleDocument): void {
    this.#commit(next);
  }

  wrapInOperator(path: string, operator: BinaryOperator, sibling: RuleNode): void {
    const existing = getNode(this.#document, path);
    if (!existing) throw new Error(`No node at ${path}.`);
    this.#commit(setNode(this.#document, path, { [operator]: [existing, sibling] } as unknown as RuleNode));
  }

  addOperand(operatorPath: string, node: RuleNode): void {
    const target = getNode(this.#document, operatorPath);
    if (!target || !isBinaryNode(target)) throw new Error(`No operator node at ${operatorPath}.`);
    const op = binaryOperator(target);
    const next = { ...target, [op]: [...operandsOf(target), node] } as RuleNode;
    this.#commit(setNode(this.#document, operatorPath, next));
  }

  removeOperand(elementPath: string): void {
    const { parentPath, step } = splitLast(elementPath);
    if (step.index === undefined) throw new Error(`${elementPath} is not an operator-array element.`);
    const parent = getNode(this.#document, parentPath);
    if (!parent || !isBinaryNode(parent)) throw new Error(`No operator node at ${parentPath}.`);
    const op = binaryOperator(parent);
    const remaining = operandsOf(parent).filter((_, i) => i !== step.index);
    const replacement = remaining.length === 1 ? remaining[0]! : ({ ...parent, [op]: remaining } as RuleNode);
    this.#commit(setNode(this.#document, parentPath, replacement));
  }

  unwrap(path: string): void {
    const node = getNode(this.#document, path);
    if (!node) throw new Error(`No node at ${path}.`);
    if (isNotNode(node)) return this.#commit(setNode(this.#document, path, node.not));
    if (isBinaryNode(node)) return this.#commit(setNode(this.#document, path, operandsOf(node)[0]!));
    throw new Error(`Node at ${path} cannot be unwrapped.`);
  }

  setDecoration(path: string, decoration: Partial<Pick<Decoration, 'whenTrue' | 'whenFalse'>>): void {
    const node = getNode(this.#document, path);
    if (!node) throw new Error(`No node at ${path}.`);
    this.#commit(setNode(this.#document, path, { ...node, ...decoration }));
  }

  setName(path: string, name: string | undefined): void {
    const node = getNode(this.#document, path);
    if (!node) throw new Error(`No node at ${path}.`);
    const next = { ...node } as RuleNode & { name?: string };
    if (name === undefined) delete next.name;
    else next.name = name;
    this.#commit(setNode(this.#document, path, next));
  }

  /**
   * Moves the subtree at `path` into `definitions[name]` and replaces it with `{ local: name }`.
   * The subtree's own `name`/`whenTrue`/`whenFalse` become the definition's — a definition's key
   * *is* its name, so a carried-over `name` would be redundant and is simply dropped. The
   * replacement reference carries no decoration of its own.
   */
  defineLocal(path: string, name: string): void {
    if (!isValidLocalName(name)) throw new Error(`Invalid definition name: ${name}.`);
    if (this.#document.definitions?.[name]) throw new Error(`Definition "${name}" already exists.`);
    if (path === definitionBodyPath(name) || path.startsWith(`${definitionPath(name)}.`)) {
      throw new Error(`Cannot define "${name}" from inside its own body.`);
    }
    const node = getNode(this.#document, path);
    if (!node) throw new Error(`No node at ${path}.`);

    const { name: _name, whenTrue, whenFalse, ...body } = node as RuleNode & Decoration;
    const definition: Definition = { rule: body as RuleNode };
    if (whenTrue !== undefined) definition.whenTrue = whenTrue;
    if (whenFalse !== undefined) definition.whenFalse = whenFalse;

    const withDefinition = structuredClone(this.#document);
    withDefinition.definitions = { ...(withDefinition.definitions ?? {}), [name]: definition };
    this.#commit(setNode(withDefinition, path, { local: name }));
  }

  /**
   * Replaces the `local` reference at `path` with a structured clone of its definition body, the
   * definition's `whenTrue`/`whenFalse` and its key reapplied as the body's `name` — reproducing
   * the pre-#234 nested-name form exactly. When no reference to the definition remains anywhere
   * (the rule or any definition body), the definition is removed.
   */
  inlineLocal(path: string): void {
    const node = getNode(this.#document, path);
    if (!node || !isLocalNode(node)) throw new Error(`No local reference at ${path}.`);
    const name = node.local;
    const definition = this.#document.definitions?.[name];
    if (!definition) throw new Error(`No definition "${name}".`);

    const inlined = { ...structuredClone(definition.rule), name } as RuleNode & Decoration;
    if (definition.whenTrue !== undefined) inlined.whenTrue = definition.whenTrue; else delete inlined.whenTrue;
    if (definition.whenFalse !== undefined) inlined.whenFalse = definition.whenFalse; else delete inlined.whenFalse;

    const next = setNode(this.#document, path, inlined as RuleNode);
    if (localReferences(next, name).length === 0 && next.definitions) {
      delete next.definitions[name];
      if (Object.keys(next.definitions).length === 0) delete next.definitions;
    }
    this.#commit(next);
  }

  /** Renames a definition's key and every `local` reference to it, in the rule and every definition. */
  renameLocal(from: string, to: string): void {
    if (!isValidLocalName(to)) throw new Error(`Invalid definition name: ${to}.`);
    if (!this.#document.definitions?.[from]) throw new Error(`No definition "${from}".`);
    if (to !== from && this.#document.definitions?.[to]) throw new Error(`Definition "${to}" already exists.`);

    let next = structuredClone(this.#document);
    const renamed: Record<string, Definition> = {};
    for (const [key, definition] of Object.entries(next.definitions ?? {})) {
      renamed[key === from ? to : key] = definition;
    }
    next.definitions = renamed;

    for (const path of localReferences(next, from)) {
      const node = getNode(next, path);
      next = setNode(next, path, { ...node, local: to } as RuleNode);
    }
    this.#commit(next);
  }

  /** Removes a definition; throws while any `local` reference to it remains. */
  removeDefinition(name: string): void {
    if (!this.#document.definitions?.[name]) throw new Error(`No definition "${name}".`);
    if (localReferences(this.#document, name).length > 0) {
      throw new Error(`Definition "${name}" is still referenced.`);
    }
    const next = structuredClone(this.#document);
    delete next.definitions![name];
    if (Object.keys(next.definitions!).length === 0) delete next.definitions;
    this.#commit(next);
  }

  /** Replaces every `{ local: name }` reference with `{ spec }` and removes the definition. */
  replaceLocalWithSpec(name: string, spec: string): void {
    if (!this.#document.definitions?.[name]) throw new Error(`No definition "${name}".`);
    let next = structuredClone(this.#document);
    for (const path of localReferences(next, name)) {
      next = setNode(next, path, { spec });
    }
    delete next.definitions![name];
    if (Object.keys(next.definitions!).length === 0) delete next.definitions;
    this.#commit(next);
  }

  /** Patches a definition's `whenTrue`/`whenFalse`; an explicit `undefined` clears that field. */
  setDefinitionDecoration(
    name: string,
    decoration: { whenTrue?: Payload | undefined; whenFalse?: Payload | undefined },
  ): void {
    const definition = this.#document.definitions?.[name];
    if (!definition) throw new Error(`No definition "${name}".`);
    const next = structuredClone(this.#document);
    const nextDefinition: Definition = { ...definition };
    if ('whenTrue' in decoration) {
      if (decoration.whenTrue === undefined) delete nextDefinition.whenTrue;
      else nextDefinition.whenTrue = decoration.whenTrue;
    }
    if ('whenFalse' in decoration) {
      if (decoration.whenFalse === undefined) delete nextDefinition.whenFalse;
      else nextDefinition.whenFalse = decoration.whenFalse;
    }
    next.definitions![name] = nextDefinition;
    this.#commit(next);
  }

  setErrors(errors: RuleError[]): void {
    this.#errors = errors;
    this.#notify();
  }

  /** Replaces the entire document as a fresh baseline: history and errors are cleared. */
  loadDocument(document: RuleDocument): void {
    this.#document = structuredClone(document);
    this.#errors = [];
    this.#undo = [];
    this.#redo = [];
    this.#notify();
  }

  undo(): void {
    const previous = this.#undo.pop();
    if (!previous) return;
    this.#redo.push(this.#document);
    this.#document = previous;
    this.#notify();
  }

  redo(): void {
    const next = this.#redo.pop();
    if (!next) return;
    this.#undo.push(this.#document);
    this.#document = next;
    this.#notify();
  }

  #commit(next: RuleDocument): void {
    this.#undo.push(this.#document);
    this.#redo = [];
    this.#document = next;
    this.#notify();
  }

  #notify(): void {
    for (const listener of this.#listeners) listener();
  }
}
