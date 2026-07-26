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
  readonly #listeners = new Set<() => void>();

  constructor(initial: RuleDocument) {
    this.#document = structuredClone(initial);
  }

  getState(): EditorState {
    return {
      document: this.#document,
      errors: this.#errors,
      canUndo: this.#undo.length > 0,
      canRedo: this.#redo.length > 0,
    };
  }

  subscribe(listener: () => void): () => void {
    this.#listeners.add(listener);
    return () => this.#listeners.delete(listener);
  }

  replaceNode(path: string, node: RuleNode): void {
    this.#commit(setNode(this.#document, path, node));
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
