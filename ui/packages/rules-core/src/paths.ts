import {
  binaryOperator, higherOrderKey, isBinaryNode, isHigherOrderNode, isNotNode,
  operandsOf, type RuleDocument, type RuleNode,
} from './document.js';

const ROOT = '$.rule';

interface Step { key: string; index?: number }

function parseSteps(path: string): Step[] {
  if (path !== ROOT && !path.startsWith(`${ROOT}.`)) {
    throw new Error(`Invalid node path: ${path}`);
  }
  const rest = path.slice(ROOT.length);
  if (rest === '') return [];
  return rest.split('.').filter(Boolean).map((token) => {
    const match = token.match(/^([A-Za-z]+)(?:\[(\d+)\])?$/);
    if (!match) throw new Error(`Invalid path token: ${token}`);
    return match[2] === undefined
      ? { key: match[1]! }
      : { key: match[1]!, index: Number(match[2]) };
  });
}

/** Rebuilds a path string from steps (inverse of parseSteps). */
export function joinSteps(basePath: string, ...appended: Step[]): string {
  return appended.reduce<string>(
    (acc, step) => (step.index === undefined ? `${acc}.${step.key}` : `${acc}.${step.key}[${step.index}]`),
    basePath,
  );
}

/** The parent path and final step of a non-root path (throws for the root). */
export function splitLast(path: string): { parentPath: string; step: Step } {
  const steps = parseSteps(path);
  const step = steps.at(-1);
  if (!step) throw new Error(`Path has no parent: ${path}`);
  return { parentPath: joinSteps(ROOT, ...steps.slice(0, -1)), step };
}

/** Resolves the node at a path, or undefined when it does not exist. */
export function getNode(document: RuleDocument, path: string): RuleNode | undefined {
  let node: RuleNode | undefined = document.rule;
  for (const { key, index } of parseSteps(path)) {
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
  const steps = parseSteps(path);
  if (steps.length === 0) {
    clone.rule = replacement;
    return clone;
  }
  let parent: Record<string, unknown> = clone.rule as unknown as Record<string, unknown>;
  for (const { key, index } of steps.slice(0, -1)) {
    const child = parent[key];
    parent = (index === undefined ? child : (child as unknown[])[index]) as Record<string, unknown>;
  }
  const last = steps.at(-1)!;
  if (last.index === undefined) parent[last.key] = replacement;
  else (parent[last.key] as RuleNode[])[last.index] = replacement;
  return clone;
}

/** Every node in the tree with its backend-shaped path, root first (pre-order). */
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
      const key = higherOrderKey(node);
      walk((node as unknown as Record<string, RuleNode>)[key]!, `${path}.${key}`);
    }
  };
  walk(document.rule, ROOT);
  return out;
}
