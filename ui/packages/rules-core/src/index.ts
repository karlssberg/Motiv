/**
 * The public surface of `@motiv-rules/core`, named export by named export. Everything here is
 * chosen: adding a symbol to this barrel is a deliberate API decision, pinned by the
 * approved-API snapshot in `test/api-surface.test.ts`, not a side effect of an `export`
 * keyword somewhere in the package. Modules may export more than the barrel re-exports; those
 * extras are internal.
 */

// The rule-document model: node shapes, and the helpers that read them.
export {
  BINARY_OPERATORS, HIGHER_ORDER_KEYS,
  nodeKind, isSpecNode, isExpressionNode, isNotNode, isBinaryNode, isHigherOrderNode,
  binaryOperator, operandsOf, higherOrderKey, higherOrderBody,
  type Payload, type Decoration, type Countable, type ArgValue,
  type SpecNode, type ExpressionNode, type NotNode,
  type AndNode, type OrNode, type XorNode, type AndAlsoNode, type OrElseNode,
  type AsAllSatisfiedNode, type AsAnySatisfiedNode, type AsNSatisfiedNode,
  type AsAtLeastNSatisfiedNode, type AsAtMostNSatisfiedNode,
  type BinaryNode, type HigherOrderNode, type RuleNode,
  type BinaryOperator, type HigherOrderKey, type NodeKind,
  type ParameterDeclaration, type RuleDocument,
} from './document.js';

// The wire contracts shared with the ASP.NET Core endpoints.
export type {
  Catalog, CatalogEntry, CatalogCollection, CatalogParameter, JsonSchema,
  RuleError, RuleErrorCode, ValidationResponse, ErrorResponse,
  ExplanationNode, EvaluationResult, ValidateRequest, EvaluateRequest,
  RuleListEntry, RuleGetResponse, RuleSaveResult,
  PropositionOrigin, PropositionListEntry, PropositionGetResponse, PropositionCreateRequest,
  DependentEntry, PropositionSaveResult, BrokenDependent,
} from './contracts.js';

// Client-side JSON Schema validation.
export { validateAgainstSchema, type SchemaViolation } from './schema.js';

// The HTTP client for the rules endpoints.
export { RulesApiClient, RulesApiError, type RulesApiClientOptions, type StoreGeneration } from './client.js';

// Path arithmetic over a rule document.
export { joinSteps, splitLast, getNode, setNode, listPaths, childPaths } from './paths.js';

// Document normalization.
export { normalizeAt } from './normalize.js';

// The subscribable editor store, and the mutations the builder performs on it.
export { RuleEditorStore, errorsForNode, type EditorState } from './editor.js';
export {
  N_QUANTIFIER_KINDS, literalCountOf,
  setBinaryOperator, setQuantifierKind, setQuantifierCollection, setQuantifierN,
} from './mutations.js';

// Live-validation orchestration.
export { createValidationController, type ValidationControllerOptions } from './validation.js';

// Evaluation-explanation projections.
export { toExplanationView, flattenExplanation, type ExplanationView, type ExplanationRow } from './explanation.js';

// Node-insertion planning.
export { insertTargetForRow, firstOperandTarget, planInsert, type InsertTarget } from './plan.js';

// The dotted-name namespace projection.
export { buildNamespaceTree, filterTree, countLeaves, type NamespaceNode } from './namespaceTree.js';

// The builder's view-state machines: the accordion, and hover/selection highlighting.
export {
  EMPTY_ACCORDION, isCollapsed, isPinned, isOpen,
  toggleCollapsed, toggleOpen, togglePin, closeAll,
  type AccordionModel,
} from './accordion.js';
export {
  EMPTY_HIGHLIGHT, setHovered, setSelected, focusedPath, type HighlightModel,
} from './highlight.js';

// The one-line summary of a rule node.
export { OPERATOR_LABELS, summarize, type NodeBadgeKind, type NodeSummary } from './nodeSummary.js';

// The generated text an accessible name is built from.
export { ACCESSIBLE_NAME_LIMIT, accessibleExpression } from './a11y.js';

// The DSL: lexing, parsing, printing, spans, and the vocabulary they share.
export {
  type TokenKind, type Token, type DslError, type NodeSpan, type ParseResult,
} from './dsl/types.js';
export {
  tokenize,
  DSL_KEYWORDS, DSL_QUANTIFIERS, DSL_TYPES,
  WORD_START_CHARS, WORD_REST_CHARS, PARAM_REST_CHARS,
} from './dsl/lexer.js';
export { parse, type ParseOptions } from './dsl/parser.js';
export { print, printInline, type PrintOptions } from './dsl/printer.js';
export { mergeDecorations } from './dsl/decorations.js';
export { rangeOfPath, type SourceRange } from './dsl/spans.js';

// Editor-neutral authoring services: token runs, completion, diagnostics, and DSL/tree sync.
export { tokenSpans, type TokenSpan } from './dsl/tokenRuns.js';
export {
  completeDsl, type CompletionItem, type CompletionItemKind, type DslCompletion,
} from './dsl/completion.js';
export { diagnosticsFor, type RuleDiagnostic } from './dsl/diagnostics.js';
export { DslSyncController, type DslSyncState, type SyncStatus } from './dslSync.js';
