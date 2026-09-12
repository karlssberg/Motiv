import {
  binaryOperator, isBinaryNode, isNotNode, operandsOf,
  type BinaryOperator, type Decoration, type RuleDocument, type RuleNode,
} from './document.js';
import type { RuleError } from './contracts.js';
import { getNode, setNode, splitLast } from './paths.js';

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
