import {
  binaryOperator, higherOrderKey, isBinaryNode, isExpressionNode, isHigherOrderNode,
  isNotNode, isSpecNode, operandsOf,
  type BinaryNode, type BinaryOperator, type HigherOrderNode, type ParameterDeclaration,
  type RuleDocument, type RuleNode,
} from '../document.js';

const INDENT = '    ';

/** Node key → DSL operator. */
const OPERATOR_TEXT: Record<BinaryOperator, string> = {
  orElse: '||', andAlso: '&&', or: '|', xor: '^', and: '&',
};

/** Binary keys ordered loosest to tightest, mirroring the parser's levels. Index is precedence. */
const PRECEDENCE: BinaryOperator[] = ['orElse', 'andAlso', 'or', 'xor', 'and'];

/** Binds tighter than every operator: leaves, quantifiers, negations, and any named node. */
const ATOM = PRECEDENCE.length;

/**
 * The connective each operator belongs to. `&`/`&&` and `|`/`||` are one connective at two
 * strengths, so nesting the tighter inside the looser reads correctly unparenthesised. Every
 * other mix is parenthesised: the precedence is C-style, which puts `|` tighter than `&&`, and
 * few readers expect that.
 */
const CONNECTIVE: Record<BinaryOperator, string> = {
  and: '&', andAlso: '&', or: '|', orElse: '|', xor: '^',
};

/** Higher-order node key → quantifier keyword. */
const QUANTIFIER_WORDS: Record<ReturnType<typeof higherOrderKey>, string> = {
  asAllSatisfied: 'all', asAnySatisfied: 'any', asNSatisfied: 'exactly',
  asAtLeastNSatisfied: 'atLeast', asAtMostNSatisfied: 'atMost',
};

/**
 * Binding tightness: higher binds tighter. A named node is either a postfix primary or is
 * parenthesised by its own `as` clause, so it always binds as an atom.
 */
function precedenceOf(node: RuleNode): number {
  if (node.name !== undefined || !isBinaryNode(node)) return ATOM;
  return PRECEDENCE.indexOf(binaryOperator(node));
}

/** True when a trailing `as` clause would bind to something narrower than the whole node. */
function nameNeedsParens(node: RuleNode): boolean {
  return isBinaryNode(node) || isNotNode(node);
}

/**
 * True when the node renders across several lines, which is exactly when it holds a quantifier —
 * the only construct with a mandatory block body.
 */
function isMultiline(node: RuleNode): boolean {
  if (isHigherOrderNode(node)) return true;
  if (isNotNode(node)) return isMultiline(node.not);
  if (isBinaryNode(node)) return operandsOf(node).some(isMultiline);
  return false;
}

/** The single child of a higher-order node. */
function bodyOf(node: HigherOrderNode): RuleNode {
  return (node as unknown as Record<string, RuleNode>)[higherOrderKey(node)]!;
}

/** Quotes a name or string default. The DSL has no escapes, so a `"` cannot be represented. */
function quote(value: string): string {
  return JSON.stringify(value);
}

/** Wraps rendered text in parentheses, breaking onto its own indented line when `broken`. */
function parenthesise(text: string, indent: string, broken: boolean): string {
  return broken ? `(\n${indent}${INDENT}${text}\n${indent})` : `(${text})`;
}

/**
 * Renders a child of an operator node, parenthesising it when the grammar would otherwise
 * regroup it, and breaking the group across lines when the surrounding expression is multi-line.
 */
function printChild(node: RuleNode, indent: string, needsParens: boolean, broken: boolean): string {
  if (!needsParens) return printNode(node, indent);
  return parenthesise(printNode(node, broken ? indent + INDENT : indent), indent, broken);
}

function printQuantifier(node: HigherOrderNode, indent: string): string {
  const count = 'n' in node ? `(${String(node.n)})` : '';
  const body = printNode(bodyOf(node), indent + INDENT);
  return `${QUANTIFIER_WORDS[higherOrderKey(node)]}${count} in ${node.path} {`
    + `\n${indent}${INDENT}${body}\n${indent}}`;
}

/**
 * True when `operand` must be parenthesised to survive reparsing inside `operator`, or to be
 * read as written. Required when it binds no tighter — a looser child would regroup, and an
 * equally loose one is a same-operator nesting the parser would flatten into this run. A tighter
 * child needs none only while it stays within the parent's connective.
 */
function operandNeedsParens(operand: RuleNode, operator: BinaryOperator): boolean {
  if (precedenceOf(operand) <= PRECEDENCE.indexOf(operator)) return true;
  if (operand.name !== undefined || !isBinaryNode(operand)) return false;
  return CONNECTIVE[binaryOperator(operand)] !== CONNECTIVE[operator];
}

/** Renders an n-ary operator as its operands joined by the operator. */
function printBinary(node: BinaryNode, indent: string): string {
  const operator = binaryOperator(node);
  const broken = isMultiline(node);
  const parts = operandsOf(node).map((operand) =>
    printChild(operand, indent, operandNeedsParens(operand, operator), broken));
  return parts.join(` ${OPERATOR_TEXT[operator]} `);
}

/** Renders a node without its `as` clause. */
function printBody(node: RuleNode, indent: string): string {
  if (isSpecNode(node)) return node.spec;
  if (isExpressionNode(node)) return `\`${node.expression}\``;
  if (isNotNode(node)) {
    const operand = node.not;
    return `!${printChild(operand, indent, precedenceOf(operand) < ATOM, isMultiline(operand))}`;
  }
  if (isHigherOrderNode(node)) return printQuantifier(node, indent);
  return printBinary(node, indent);
}

/** Renders a node and its `as` clause, at an indentation its continuation lines start from. */
function printNode(node: RuleNode, indent: string): string {
  const name = node.name;
  if (name === undefined) return printBody(node, indent);
  if (!nameNeedsParens(node)) return `${printBody(node, indent)} as ${quote(name)}`;

  const broken = isMultiline(node);
  const body = printBody(node, broken ? indent + INDENT : indent);
  return `${parenthesise(body, indent, broken)} as ${quote(name)}`;
}

function printDefault(value: NonNullable<ParameterDeclaration['default']>): string {
  return typeof value === 'string' ? quote(value) : String(value);
}

/** Renders the leading `param` declarations, including the blank line that closes the block. */
function printParameters(parameters: RuleDocument['parameters']): string {
  const entries = Object.entries(parameters ?? {});
  if (entries.length === 0) return '';
  const lines = entries.map(([name, declaration]) => {
    const suffix = declaration.default === undefined ? '' : ` = ${printDefault(declaration.default)}`;
    return `param ${name}: ${declaration.type}${suffix}`;
  });
  return `${lines.join('\n')}\n\n`;
}

/** Reprints a rule document as canonical DSL text — the inverse of `parse`. */
export function print(document: RuleDocument): string {
  return `${printParameters(document.parameters)}${printNode(document.rule, '')}`;
}
