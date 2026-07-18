/** A whenTrue/whenFalse payload: an explanation string or a metadata object. */
export type Payload = string | Record<string, unknown>;

/** Optional decoration any node may carry. */
export interface Decoration {
  whenTrue?: Payload;
  whenFalse?: Payload;
  name?: string;
}

/** An N value: a literal count or a "@param" reference. */
export type Countable = number | string;

export interface SpecNode extends Decoration { spec: string }
export interface ExpressionNode extends Decoration { expression: string }
export interface NotNode extends Decoration { not: RuleNode }
export interface AndNode extends Decoration { and: RuleNode[] }
export interface OrNode extends Decoration { or: RuleNode[] }
export interface XorNode extends Decoration { xor: RuleNode[] }
export interface AndAlsoNode extends Decoration { andAlso: RuleNode[] }
export interface OrElseNode extends Decoration { orElse: RuleNode[] }
export interface AsAllSatisfiedNode extends Decoration { asAllSatisfied: RuleNode; path?: string }
export interface AsAnySatisfiedNode extends Decoration { asAnySatisfied: RuleNode; path?: string }
export interface AsNSatisfiedNode extends Decoration { asNSatisfied: RuleNode; n: Countable; path?: string }
export interface AsAtLeastNSatisfiedNode extends Decoration { asAtLeastNSatisfied: RuleNode; n: Countable; path?: string }
export interface AsAtMostNSatisfiedNode extends Decoration { asAtMostNSatisfied: RuleNode; n: Countable; path?: string }

export type BinaryNode = AndNode | OrNode | XorNode | AndAlsoNode | OrElseNode;
export type HigherOrderNode =
  | AsAllSatisfiedNode | AsAnySatisfiedNode | AsNSatisfiedNode
  | AsAtLeastNSatisfiedNode | AsAtMostNSatisfiedNode;

export type RuleNode =
  | SpecNode | ExpressionNode | NotNode | BinaryNode | HigherOrderNode;

/** A parameter declaration in a rule document. */
export interface ParameterDeclaration {
  type: 'integer' | 'number' | 'string' | 'boolean';
  default?: number | string | boolean;
}

/** A complete externalized Motiv rule document. */
export interface RuleDocument {
  $schema?: string;
  name?: string;
  parameters?: Record<string, ParameterDeclaration>;
  rule: RuleNode;
}

export const BINARY_OPERATORS = ['and', 'or', 'xor', 'andAlso', 'orElse'] as const;
export type BinaryOperator = (typeof BINARY_OPERATORS)[number];

const HIGHER_ORDER_KEYS = [
  'asAllSatisfied', 'asAnySatisfied', 'asNSatisfied',
  'asAtLeastNSatisfied', 'asAtMostNSatisfied',
] as const;
type HigherOrderKey = (typeof HIGHER_ORDER_KEYS)[number];

/** The discriminant of a node: which operator or leaf it is. */
export type NodeKind = 'spec' | 'expression' | 'not' | BinaryOperator | HigherOrderKey;

const KIND_ORDER: readonly NodeKind[] = [
  'spec', 'expression', 'not', ...BINARY_OPERATORS, ...HIGHER_ORDER_KEYS,
];

/** Returns the discriminant key of a node. */
export function nodeKind(node: RuleNode): NodeKind {
  for (const key of KIND_ORDER) {
    if (key in node) return key;
  }
  throw new Error(`Unrecognised rule node: ${JSON.stringify(node)}`);
}

export function isSpecNode(node: RuleNode): node is SpecNode { return 'spec' in node; }
export function isExpressionNode(node: RuleNode): node is ExpressionNode { return 'expression' in node; }
export function isNotNode(node: RuleNode): node is NotNode { return 'not' in node; }

export function isBinaryNode(node: RuleNode): node is BinaryNode {
  return BINARY_OPERATORS.some((op) => op in node);
}

export function isHigherOrderNode(node: RuleNode): node is HigherOrderNode {
  return HIGHER_ORDER_KEYS.some((key) => key in node);
}

/** The operator of a binary node. */
export function binaryOperator(node: BinaryNode): BinaryOperator {
  const op = BINARY_OPERATORS.find((candidate) => candidate in node);
  if (!op) throw new Error('Node is not a binary operator node.');
  return op;
}

/** The operands array of a binary node. */
export function operandsOf(node: BinaryNode): RuleNode[] {
  return (node as Record<BinaryOperator, RuleNode[]>)[binaryOperator(node)];
}

/** The single child key of a higher-order node. */
export function higherOrderKey(node: HigherOrderNode): HigherOrderKey {
  const key = HIGHER_ORDER_KEYS.find((candidate) => candidate in node);
  if (!key) throw new Error('Node is not a higher-order node.');
  return key;
}
