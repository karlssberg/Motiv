import {
  binaryOperator, higherOrderBody, higherOrderKey, isBinaryNode, isHigherOrderNode,
  isLocalNode, isNotNode, operandsOf, type RuleDocument, type RuleNode,
} from './document.js';
import { LOCAL_NAME_PATTERN } from './localNames.js';

const RULE_ROOT = '$.rule';

/** The root of the `definitions` map — a definition itself is not a node; see {@link definitionPath}. */
export const DEFINITIONS_ROOT = '$.definitions';

// Keys that would mutate the prototype chain if used as a dynamic property target.
const FORBIDDEN_KEYS = new Set(['__proto__', 'prototype', 'constructor']);

interface Step { key: string; index?: number }

/** The path of a definition itself: `$.definitions.<name>`. Not a node path — see {@link definitionBodyPath}. */
export function definitionPath(name: string): string {
  return `${DEFINITIONS_ROOT}.${name}`;
}

/** The path of a definition's rule body: `$.definitions.<name>.rule`, the root a node path resolves from. */
export function definitionBodyPath(name: string): string {
  return `${definitionPath(name)}.rule`;
}

/** The `<name>` of the definition a path is under, or undefined when the path is not under one. */
export function definitionNameOf(path: string): string | undefined {
  if (!path.startsWith(`${DEFINITIONS_ROOT}.`)) return undefined;
  const name = path.slice(DEFINITIONS_ROOT.length + 1).split('.')[0];
  return name || undefined;
}

/** A parse that failed, carrying the message the throwing caller raises. */
interface PathRefusal { message: string }

function refused(result: { message: string } | object): result is PathRefusal {
  return 'message' in result;
}

function readStepTokens(rest: string): { steps: Step[] } | PathRefusal {
  const steps: Step[] = [];
  if (rest === '') return { steps };
  for (const token of rest.split('.').filter(Boolean)) {
    // Reject prototype-polluting keys before any dynamic property access.
    const keyCandidate = token.split('[')[0]!;
    if (FORBIDDEN_KEYS.has(keyCandidate)) return { message: `Forbidden path key: ${keyCandidate}` };
    const match = token.match(/^([A-Za-z]+)(?:\[(\d+)\])?$/);
    if (!match) return { message: `Invalid path token: ${token}` };
    steps.push(match[2] === undefined
      ? { key: match[1]! }
      : { key: match[1]!, index: Number(match[2]) });
  }
  return { steps };
}

/**
 * The node path resolves from — `$.rule` or a definition's `$.definitions.<name>.rule` — and the
 * ordinary steps below it, or why the path names no node. A definition path without `.rule` (the
 * definition itself, not its body) is not a node and is refused here, as is a name that fails
 * {@link LOCAL_NAME_PATTERN}.
 *
 * Answers rather than throws so {@link isNodePath} can ask the same question without a `try`, and
 * so both agree by construction rather than by two copies of the rule kept in step.
 */
function readPath(path: string): { base: string; steps: Step[] } | PathRefusal {
  if (path === RULE_ROOT || path.startsWith(`${RULE_ROOT}.`)) {
    const tokens = readStepTokens(path.slice(RULE_ROOT.length));
    return refused(tokens) ? tokens : { base: RULE_ROOT, steps: tokens.steps };
  }
  if (path.startsWith(`${DEFINITIONS_ROOT}.`)) {
    const rest = path.slice(DEFINITIONS_ROOT.length + 1);
    const [name, ...afterParts] = rest.split('.');
    if (!name || !LOCAL_NAME_PATTERN.test(name)) return { message: `Invalid path token: ${name ?? ''}` };
    if (FORBIDDEN_KEYS.has(name)) return { message: `Forbidden path key: ${name}` };
    const after = afterParts.join('.');
    const base = definitionBodyPath(name);
    if (after === 'rule') return { base, steps: [] };
    if (after.startsWith('rule.')) {
      const tokens = readStepTokens(after.slice('rule'.length));
      return refused(tokens) ? tokens : { base, steps: tokens.steps };
    }
    return { message: `Invalid node path: ${path}` };
  }
  return { message: `Invalid node path: ${path}` };
}

/**
 * Whether a path names a node — `$.rule…`, or a definition's body `$.definitions.<name>.rule…`.
 *
 * False for a definition itself (`$.definitions.<name>`) and its non-`rule` fields, which are real,
 * addressable places a span or an error can sit but hold no node. Anything that walks a collection
 * of paths and resolves each one has to ask first: {@link getNode} throws for the rest.
 */
export function isNodePath(path: string): boolean {
  return !refused(readPath(path));
}

function parsePath(path: string): { base: string; steps: Step[] } {
  const result = readPath(path);
  if (refused(result)) throw new Error(result.message);
  return result;
}

/** Rebuilds a path string from steps (inverse of parsePath). */
export function joinSteps(basePath: string, ...appended: Step[]): string {
  return appended.reduce<string>(
    (acc, step) => (step.index === undefined ? `${acc}.${step.key}` : `${acc}.${step.key}[${step.index}]`),
    basePath,
  );
}

/** The parent path and final step of a non-root path (throws for a root). */
export function splitLast(path: string): { parentPath: string; step: Step } {
  const { base, steps } = parsePath(path);
  const step = steps.at(-1);
  if (!step) throw new Error(`Path has no parent: ${path}`);
  return { parentPath: joinSteps(base, ...steps.slice(0, -1)), step };
}

/** The node a path's base resolves from — `document.rule`, or a definition's rule body. */
function resolveBase(document: RuleDocument, base: string): RuleNode | undefined {
  if (base === RULE_ROOT) return document.rule;
  const name = definitionNameOf(base)!;
  return document.definitions?.[name]?.rule;
}

/** Resolves the node at a path, or undefined when it does not exist. */
export function getNode(document: RuleDocument, path: string): RuleNode | undefined {
  const { base, steps } = parsePath(path);
  let node: RuleNode | undefined = resolveBase(document, base);
  for (const { key, index } of steps) {
    if (!node) return undefined;
    const child: unknown = (node as unknown as Record<string, unknown>)[key];
    node = index === undefined
      ? (child as RuleNode | undefined)
      : (Array.isArray(child) ? (child[index] as RuleNode | undefined) : undefined);
  }
  return node;
}

/** Returns a new document with the node at a path replaced. */
export function setNode(document: RuleDocument, path: string, replacement: RuleNode): RuleDocument {
  const clone = structuredClone(document);
  const { base, steps } = parsePath(path);

  let parent: Record<string, unknown>;
  if (base === RULE_ROOT) {
    if (steps.length === 0) {
      clone.rule = replacement;
      return clone;
    }
    parent = clone.rule as unknown as Record<string, unknown>;
  } else {
    const name = definitionNameOf(base)!;
    const definition = clone.definitions?.[name];
    if (!definition) throw new Error(`Invalid node path: ${path}`);
    if (steps.length === 0) {
      definition.rule = replacement;
      return clone;
    }
    parent = definition.rule as unknown as Record<string, unknown>;
  }

  for (const { key, index } of steps.slice(0, -1)) {
    // parsePath already rejects these; repeated inline so the traversal below provably
    // cannot step onto Object.prototype and feed it to the final assignment.
    if (key === '__proto__' || key === 'prototype' || key === 'constructor') {
      throw new Error(`Forbidden path key: ${key}`);
    }
    const child = parent[key];
    parent = (index === undefined ? child : (child as unknown[])[index]) as Record<string, unknown>;
  }
  // parsePath already rejects these; repeated inline on a local so the dynamic assignment
  // below is locally provably safe (recognized as a sanitizer by static analyzers).
  const { key, index } = steps.at(-1)!;
  if (key === '__proto__' || key === 'prototype' || key === 'constructor') {
    throw new Error(`Forbidden path key: ${key}`);
  }
  if (index === undefined) parent[key] = replacement;
  else (parent[key] as RuleNode[])[index] = replacement;
  return clone;
}

/**
 * Every node in the tree with its backend-shaped path, root first (pre-order): `document.rule`
 * from `$.rule`, then every definition's body from its {@link definitionBodyPath}, in key order.
 */
export function listPaths(document: RuleDocument): Array<{ path: string; node: RuleNode }> {
  const out: Array<{ path: string; node: RuleNode }> = [];
  const walk = (node: RuleNode, path: string): void => {
    out.push({ path, node });
    if (isNotNode(node)) {
      walk(node.not, `${path}.not`);
    } else if (isBinaryNode(node)) {
      const op = binaryOperator(node);
      operandsOf(node).forEach((child, i) => walk(child, `${path}.${op}[${i}]`));
    } else if (isHigherOrderNode(node)) {
      walk(higherOrderBody(node), `${path}.${higherOrderKey(node)}`);
    }
  };
  walk(document.rule, RULE_ROOT);
  for (const name of Object.keys(document.definitions ?? {})) {
    walk(document.definitions![name]!.rule, definitionBodyPath(name));
  }
  return out;
}

/** The paths of every `{ local: name }` node, rule first then definitions in key order. */
export function localReferences(document: RuleDocument, name: string): string[] {
  return listPaths(document)
    .filter(({ node }) => isLocalNode(node) && node.local === name)
    .map(({ path }) => path);
}

/** The child node paths of a rule node, in the same order the document walks them. */
export function childPaths(node: RuleNode, path: string): string[] {
  if (isNotNode(node)) return [`${path}.not`];
  if (isBinaryNode(node)) {
    const op = binaryOperator(node);
    return operandsOf(node).map((_, i) => `${path}.${op}[${i}]`);
  }
  if (isHigherOrderNode(node)) return [`${path}.${higherOrderKey(node)}`];
  return [];
}
