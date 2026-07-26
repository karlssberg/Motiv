import {
  binaryOperator, higherOrderBody, higherOrderKey, isBinaryNode, isExpressionNode,
  isHigherOrderNode, isNotNode, isSpecNode, operandsOf,
  type BinaryNode, type BinaryOperator, type HigherOrderKey, type HigherOrderNode,
  type NotNode, type ParameterDeclaration, type RuleDocument, type RuleNode,
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
 * How a node is laid out. `'block'` breaks quantifiers and the groups containing them across
 * lines; `'inline'` keeps everything on one, for rendering a node inside a single-line row.
 */
type Layout = 'block' | 'inline';

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
const QUANTIFIER_WORDS: Record<HigherOrderKey, string> = {
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
 * the only construct with a mandatory block body. Under `'inline'` nothing is ever multi-line,
 * which is what stops {@link parenthesise} breaking groups apart.
 */
function isMultiline(node: RuleNode, layout: Layout): boolean {
  if (layout === 'inline') return false;
  if (isHigherOrderNode(node)) return true;
  if (isNotNode(node)) return isMultiline(node.not, layout);
  if (isBinaryNode(node)) return operandsOf(node).some((operand) => isMultiline(operand, layout));
  return false;
}

/** Quotes a name or string default. The DSL has no escapes, so a `"` cannot be represented. */
function quote(value: string): string {
  return JSON.stringify(value);
}

/**
 * Wraps a group in parentheses. When `broken` the contents move onto their own line, indented
 * one level; `render` is given the indentation its continuation lines start from.
 */
function parenthesise(indent: string, broken: boolean, render: (indent: string) => string): string {
  if (!broken) return `(${render(indent)})`;
  const inner = indent + INDENT;
  return `(\n${inner}${render(inner)}\n${indent})`;
}

/**
 * Renders a child of an operator node, parenthesising it when the grammar would otherwise
 * regroup it, and breaking the group across lines when the surrounding expression is multi-line.
 */
function printChild(
  node: RuleNode, indent: string, needsParens: boolean, broken: boolean, layout: Layout,
): string {
  if (!needsParens) return printNode(node, indent, layout);
  return parenthesise(indent, broken, (inner) => printNode(node, inner, layout));
}

function printQuantifier(node: HigherOrderNode, indent: string, layout: Layout): string {
  const count = 'n' in node ? `(${String(node.n)})` : '';
  const head = `${QUANTIFIER_WORDS[higherOrderKey(node)]}${count} in ${node.path}`;
  if (layout === 'inline') return `${head} { ${printNode(higherOrderBody(node), indent, layout)} }`;
  const inner = indent + INDENT;
  return `${head} {\n${inner}${printNode(higherOrderBody(node), inner, layout)}\n${indent}}`;
}

/** Renders `!operand`, parenthesising an operand that binds looser than the negation. */
function printNegation(node: NotNode, indent: string, layout: Layout): string {
  const operand = node.not;
  const needsParens = precedenceOf(operand) < ATOM;
  return `!${printChild(operand, indent, needsParens, isMultiline(operand, layout), layout)}`;
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
function printBinary(node: BinaryNode, indent: string, layout: Layout): string {
  const operator = binaryOperator(node);
  const broken = isMultiline(node, layout);
  const parts = operandsOf(node).map((operand) =>
    printChild(operand, indent, operandNeedsParens(operand, operator), broken, layout));
  return parts.join(` ${OPERATOR_TEXT[operator]} `);
}

/** Renders a node without its `as` clause. */
function printBody(node: RuleNode, indent: string, layout: Layout): string {
  if (isSpecNode(node)) return node.spec;
  if (isExpressionNode(node)) return `\`${node.expression}\``;
  if (isNotNode(node)) return printNegation(node, indent, layout);
  if (isHigherOrderNode(node)) return printQuantifier(node, indent, layout);
  return printBinary(node, indent, layout);
}

/** Renders a node and its `as` clause, at an indentation its continuation lines start from. */
function printNode(node: RuleNode, indent: string, layout: Layout): string {
  const name = node.name;
  if (name === undefined) return printBody(node, indent, layout);
  if (!nameNeedsParens(node)) return `${printBody(node, indent, layout)} as ${quote(name)}`;

  const group = parenthesise(
    indent, isMultiline(node, layout), (inner) => printBody(node, inner, layout),
  );
  return `${group} as ${quote(name)}`;
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
  return `${printParameters(document.parameters)}${printNode(document.rule, '', 'block')}`;
}

/**
 * Renders a single node as one line of DSL, for showing it inside a row. Quantifier bodies are
 * braced on the same line rather than broken across several.
 *
 * `parse(printInline(node)).document.rule` deep-equals `node`, which is what makes a rendered
 * row safe to hand back to the parser after editing.
 */
export function printInline(node: RuleNode): string {
  return printNode(node, '', 'inline');
}
