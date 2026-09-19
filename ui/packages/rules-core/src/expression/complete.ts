import type { JsonSchema } from '../contracts.js';
import type { CompletionItem, DslCompletion } from '../dsl/completion.js';
import { elementOf, fieldsOf, isCollection, typeName, withVar, type LeafScope } from './scope.js';

const METHODS = ['where', 'any', 'all', 'count', 'sum', 'min', 'max'] as const;

/**
 * `orders` → `o`: the variable name a template introduces for elements of a collection. A chain
 * with a method call (`orders.where(o => o.total > 1)`) names its *leading* collection — the text
 * up to the first `.` or `(` — since everything after that is calls on `orders`, not a deeper
 * field path. A plain member chain with no call (`customer.orders`) has no such leading root to
 * single out, so it instead names its *last* segment, the collection actually being iterated.
 */
export function elementVar(receiver: string): string {
  const base = receiver.includes('(')
    ? receiver.replace(/[.(].*$/s, '')
    : (receiver.split('.').pop() ?? receiver);
  return base[0]?.toLowerCase() ?? 'x';
}

/** Types `a.b.where(...).sum(...)`-shaped text; undefined when any step is unknown. */
function typeChain(chain: string, scope: LeafScope): JsonSchema | undefined {
  const segments: string[] = [];
  let depth = 0; let current = '';
  for (const c of chain) {
    if (c === '(') depth++;
    if (c === ')') depth--;
    if (c === '.' && depth === 0) { segments.push(current); current = ''; continue; }
    current += c;
  }
  segments.push(current);
  let schema: JsonSchema | undefined;
  segments.forEach((segment, index) => {
    const name = segment.replace(/\(.*$/s, '');
    const isCall = segment.includes('(');
    if (index === 0) { schema = scope.vars[name]?.schema ?? scope.model.properties?.[name]; return; }
    if (!schema) return;
    if (isCall) {
      if (!isCollection(schema) || !(METHODS as readonly string[]).includes(name)) { schema = undefined; return; }
      schema = name === 'where' ? schema : name === 'any' || name === 'all' ? { type: 'boolean' } : name === 'count' ? { type: 'integer', format: 'int32' } : { type: 'number' };
      return;
    }
    schema = schema.properties?.[name];
  });
  return schema;
}

/** The receiver chain ending just before `end` (the index of a `.`), walking back over balanced parens. */
function chainBefore(text: string, end: number): string {
  let i = end; let depth = 0;
  while (i > 0) {
    const c = text[i - 1]!;
    if (c === ')') { depth++; i--; continue; }
    if (c === '(') { if (depth === 0) break; depth--; i--; continue; }
    if (depth > 0 || /[A-Za-z0-9_.@]/.test(c)) { i--; continue; }
    break;
  }
  return text.slice(i, end);
}

/** Lambda variables bound before `pos`, each typed as the element of its receiver. */
function varsBefore(text: string, pos: number, scope: LeafScope): LeafScope {
  let scoped = scope;
  const head = text.slice(0, pos);
  for (const match of head.matchAll(/\.(where|any|all|count|sum|min|max)\(\s*([A-Za-z_]\w*)\s*=>/g)) {
    const receiver = chainBefore(head, match.index);
    const element = elementOf(typeChain(receiver, scoped));
    if (element) scoped = withVar(scoped, match[2]!, element, receiver);
  }
  return scoped;
}

function prefixed(options: CompletionItem[], prefix: string, from: number): DslCompletion | null {
  const lower = prefix.toLowerCase();
  const matching = options.filter((o) => o.label.toLowerCase().startsWith(lower));
  if (matching.length === 0) return null;
  return { from, options: matching, isValidFor: (word) => word.toLowerCase().startsWith(lower) };
}

/** What can be typed at `cursor` in a leaf. */
export function completeLeaf(text: string, cursor: number, scope: LeafScope): DslCompletion | null {
  const head = text.slice(0, cursor);
  const scoped = varsBefore(text, cursor, scope);

  const param = /@(\w*)$/.exec(head);
  if (param) {
    const options = Object.entries(scope.parameters).map(([name, p]): CompletionItem => ({ label: `@${name}`, kind: 'parameter', detail: p.type }));
    return prefixed(options, param[0], cursor - param[0].length);
  }

  const member = /\.([A-Za-z_]\w*)?$/.exec(head);
  if (member) {
    const dot = cursor - member[0].length;
    const receiver = chainBefore(head, dot);
    const schema = typeChain(receiver, scoped);
    if (!schema) return null;
    if (isCollection(schema)) {
      const v = elementVar(receiver);
      const options = METHODS.map((m): CompletionItem => {
        const insert = m === 'count' ? 'count()' : `${m}(${v} => ${v}.)`;
        const detail = m === 'where' ? 'collection' : m === 'any' || m === 'all' ? 'bool' : m === 'count' ? 'int' : 'number';
        // `count()` takes no lambda, so the caret belongs after the closing paren, not inside it.
        const caretOffset = m === 'count' ? insert.length : insert.length - 1;
        return { label: m, kind: 'method', detail, insert, caretOffset };
      });
      return prefixed(options, member[1] ?? '', dot + 1);
    }
    const options = fieldsOf(schema).map((f): CompletionItem => ({ label: f.name, kind: isCollection(f.schema) ? 'collection' : 'field', detail: typeName(f.schema) }));
    return prefixed(options, member[1] ?? '', dot + 1);
  }

  const word = /(?:^|[^\w.@])([A-Za-z_]\w*)?$/.exec(head);
  if (!word) return null;
  const prefix = word[1] ?? '';
  const options: CompletionItem[] = [
    ...Object.entries(scoped.vars).map(([name, v]): CompletionItem => ({ label: name, kind: 'variable', detail: `element of ${v.of}` })),
    ...fieldsOf(scope.model).map((f): CompletionItem => ({ label: f.name, kind: isCollection(f.schema) ? 'collection' : 'field', detail: typeName(f.schema) })),
    ...Object.entries(scope.parameters).map(([name, p]): CompletionItem => ({ label: `@${name}`, kind: 'parameter', detail: p.type })),
  ];
  return prefixed(options, prefix, cursor - prefix.length);
}
