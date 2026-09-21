import type { ArgValue, ParameterDeclaration, RuleDocument } from './document.js';

/** One declared parameter of a parameterised spec, in declaration order. */
export interface CatalogParameter {
  name: string;
  /** The scalar type, matching the `parameterDeclaration.type` enum in rule.v1.json. */
  type: ParameterDeclaration['type'];
  /** The default value, absent when the parameter is required. */
  default?: ArgValue;
}

/** One catalog listing for a registered specification. */
export interface CatalogEntry {
  name: string;
  modelType: string;
  metadataType: string;
  isAsync: boolean;
  description?: string | null;
  /** Whether the spec is compiled, overridden by an authored document, or authored. */
  origin: PropositionOrigin;
  /**
   * The spec's declared parameters, in order — `null` for a plain (non-parameterised)
   * registration. Order is a hint for authoring positionally; the stored document is always named.
   */
  parameters?: CatalogParameter[] | null;
}

/** One catalog listing for a registered collection projection. */
export interface CatalogCollection {
  path: string;
  parentModelType: string;
  elementModelType: string;
}

/**
 * A minimal structural JSON Schema — the subset the backend's
 * `JsonSchemaExporter` emits for POCO metadata and model types.
 * Unknown keywords are preserved via the index signature and ignored
 * by {@link validateAgainstSchema} (permissive-by-default).
 */
export interface JsonSchema {
  /** One JSON type name, or a union (may include `"null"` and `"integer"`). */
  type?: string | string[];
  properties?: Record<string, JsonSchema>;
  required?: string[];
  items?: JsonSchema;
  enum?: unknown[];
  /** ECMAScript regex applied to string values (unanchored, per JSON Schema). */
  pattern?: string;
  /** The CLR numeric kind the host stamps on numeric fields: int32, int64, single, double, decimal. */
  format?: string;
  [keyword: string]: unknown;
}

/**
 * The full catalog: registered specs, collections, and type schemas.
 * The schema maps are optional so existing fixtures and empty-catalog
 * placeholders remain valid; the backend always sends both.
 */
export interface Catalog {
  specs: CatalogEntry[];
  collections: CatalogCollection[];
  /** JSON Schemas for whenTrue/whenFalse payloads, keyed by the `metadataType` strings entries carry (e.g. `"String"`). */
  metadataTypes?: Record<string, JsonSchema>;
  /** JSON Schemas for model JSON, keyed by registered model id (e.g. `"customer"`). */
  modelTypes?: Record<string, JsonSchema>;
}

/** Stable machine-readable rule-document error codes (mirrors Motiv.Serialization.RuleErrorCode). */
export type RuleErrorCode =
  | 'InvalidNode' | 'UnknownSpec' | 'ModelTypeMismatch' | 'MetadataTypeMismatch'
  | 'MixedWhenTrueFalseKinds' | 'ExpressionsNotEnabled' | 'AsyncSpecInSyncLoad'
  | 'DocumentTooLarge' | 'MissingParameter' | 'SurplusParameter'
  | 'ParameterTypeMismatch' | 'UnknownParameterReference' | 'UnknownCollection'
  | 'AsyncSpecInHigherOrder' | 'PolicyRequired' | 'UnknownLocal' | 'InvalidLocalName'
  | 'InvalidExpression' | 'UnknownField' | 'UnknownMethod' | 'ExpressionTypeMismatch' | 'ExpressionRequiresMetadata';

/** A half-open character range `[start, end)` inside an expression leaf's text. */
export interface RuleTextRange { start: number; end: number }

/** What the server's checker learned about one place in a leaf: a literal's solved type, the leaf's result type, or a warning. */
export interface RuleLeafFact {
  path: string;
  range: RuleTextRange;
  text: string;
  type: string;
  from: string | null;
  isWarning: boolean;
  message: string | null;
}

/** A single validation or load error. */
export interface RuleError {
  path: string;
  code: RuleErrorCode;
  message: string;
  range?: RuleTextRange | null;
}

/** The body returned by the validate endpoint (and by evaluate on an invalid document). */
export interface ValidationResponse {
  errors: RuleError[];
  facts?: RuleLeafFact[];
}

/** A request-level error envelope (e.g. unknown model type). */
export interface ErrorResponse {
  error: string;
}

/** A node in the de-noised causal explanation tree. */
export interface ExplanationNode {
  assertions: string[];
  underlying: ExplanationNode[];
}

/** The serialized outcome of an evaluation. */
export interface EvaluationResult {
  satisfied: boolean;
  reason: string;
  assertions: string[];
  values: string[];
  justification: string;
  explanation: ExplanationNode;
}

/** Request body for the validate endpoint. */
export interface ValidateRequest {
  modelType: string;
  document: RuleDocument;
  /** Validate for an asynchronous load, so the document may reference async specs. */
  isAsync?: boolean;
}

/** Request body for the evaluate endpoint. */
export interface EvaluateRequest {
  modelType: string;
  document: RuleDocument;
  model: unknown;
}

/** Request body for the live-rule evaluate endpoint, POST /rules/{name}/evaluate. */
export interface RuleEvaluateRequest {
  model: unknown;
}

/** One live-rule listing from GET /rules. */
export interface RuleListEntry {
  name: string;
  modelType: string;
  metadataType: string;
  isAsync: boolean;
  isPolicy: boolean;
  version: number;
  description?: string | null;
}

/** A rule's current document (null while on a code-defined default) and version. */
export interface RuleGetResponse {
  document: RuleDocument | null;
  version: number;
}

/** The outcome of a save or revert: updated, a version conflict, or validation errors. */
export type RuleSaveResult =
  | { outcome: 'updated'; version: number }
  | { outcome: 'conflict'; currentVersion: number }
  | { outcome: 'invalid'; errors: RuleError[] };

/** Where a proposition's current definition comes from. */
export type PropositionOrigin = 'Compiled' | 'Overridden' | 'Authored';

/** One proposition in scope, compiled or authored. */
export interface PropositionListEntry {
  name: string;
  modelType: string;
  metadataType: string;
  isAsync: boolean;
  origin: PropositionOrigin;
  version: number;
  description: string | null;
  /**
   * Binding errors that excluded an authored document from the effective set; empty when it bound.
   * Orthogonal to `origin` — an overridden or an authored proposition can each be quarantined.
   */
  quarantine: RuleError[];
}

/** One proposition's authored document and version. */
export interface PropositionGetResponse {
  /** Null when the name is served by a compiled spec. */
  document: RuleDocument | null;
  /** 0 when the proposition is purely compiled. */
  version: number;
  origin: PropositionOrigin;
  /** Whether deleting reverts to a compiled spec rather than removing outright. */
  hasCompiledDefault: boolean;
}

/** A request to author a new proposition. */
export interface PropositionCreateRequest {
  name: string;
  modelType: string;
  document: RuleDocument;
  description: string | null;
}

/** One node that would be rebound by editing a proposition. */
export interface DependentEntry {
  name: string;
  kind: 'rule' | 'proposition';
}

/**
 * The outcome of a proposition write. Every expected failure is a value rather than a throw, so the
 * UI can render it — `errors` are faults in the submitted document, `brokenDependents` are the
 * dependents the edit would have stopped binding, and the two are distinct because an error's path
 * points into *this* document and cannot address a break elsewhere.
 */
export type PropositionSaveResult =
  | { outcome: 'saved'; version: number }
  | { outcome: 'conflict'; currentVersion: number }
  | { outcome: 'invalid'; errors: RuleError[]; brokenDependents: BrokenDependent[] }
  | { outcome: 'nameTaken' }
  | { outcome: 'referenced'; referrers: string[] };

/** A dependent an attempted edit would have stopped binding. */
export interface BrokenDependent {
  name: string;
  kind: 'rule' | 'proposition';
  errors: RuleError[];
}

/** One scenario of a rule: a named sample model, as the API lists it. `model` is the text as stored, JSON or not. */
export interface ScenarioEntry {
  id: string;
  name: string;
  model: string;
  /** Null for a sample to look at; set for a test to hold. */
  expectedSatisfied: boolean | null;
  /** The logged decision this scenario was saved from, when it was. */
  sourceDecisionId: string | null;
  version: number;
  author: string;
  timestampUtc: string;
}

/** A create (`baseVersion` 0) or replace of one scenario. */
export interface ScenarioPutRequest {
  name: string;
  model: string;
  expectedSatisfied: boolean | null;
  sourceDecisionId: string | null;
  baseVersion: number;
}

/** The outcome of a scenario write; a stale base version is a value, not a throw. */
export type ScenarioSaveResult =
  | { outcome: 'saved'; version: number }
  | { outcome: 'conflict'; currentVersion: number };
