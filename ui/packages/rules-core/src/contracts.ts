import type { RuleDocument } from './document.js';

/** One catalog listing for a registered specification. */
export interface CatalogEntry {
  name: string;
  modelType: string;
  metadataType: string;
  isAsync: boolean;
  description?: string | null;
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
  | 'AsyncSpecInHigherOrder' | 'PolicyRequired';

/** A single validation or load error. */
export interface RuleError {
  path: string;
  code: RuleErrorCode;
  message: string;
}

/** The body returned by the validate endpoint (and by evaluate on an invalid document). */
export interface ValidationResponse {
  errors: RuleError[];
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
}

/** Request body for the evaluate endpoint. */
export interface EvaluateRequest {
  modelType: string;
  document: RuleDocument;
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
