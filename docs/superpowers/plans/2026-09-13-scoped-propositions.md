# Scoped propositions — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax.
> Written before the implementation and landed with it, per `CLAUDE.md`.

**Goal:** A rule or proposition document can define named, document-local propositions in a
`definitions` block, reference them with a `local` node, and author them in the DSL as
`let name = expr`; Studio stops naming nested nodes inline and offers Extract / Inline / Promote
instead.

**Architecture:** The document model grows one map and one node kind, mirrored in the JSON schema,
the TypeScript core and the C# parser. The C# parser resolves every `local` to its definition and
rejects unknown names and cycles *before* binding, so the four binders change by one switch arm
each and bind a local by binding its definition at the reference site. The DSL treats `let` as a
second preamble statement beside `param`, pre-collecting declared names so a bare word resolves at
parse time. Studio's builder gains the actions and a definitions panel; the DSL view gains a
quick-fix that lifts a nested `as "name"` into a `let`.

**Tech stack:** C# (.NET 8/9/10 + netstandard2.0, xunit + Shouldly), TypeScript
(`@motiv-rules/core`, vitest), React 18 (`@motiv-rules/react`, Studio, Testing Library, Playwright
+ axe), JSON Schema 2020-12 (`schemas/rule.v1.json`, checked by both JsonSchema.Net and Ajv).

**Spec:** `docs/superpowers/specs/2026-09-13-scoped-propositions-design.md`

## Global constraints

- **Local-name grammar**, everywhere the same: first character `A-Za-z_`, then `A-Za-z0-9_-`.
  No dot, no space. C# `SpecRegistry.IsValidName` is the catalog rule; locals are one segment of
  it minus nothing (a segment already forbids dots).
- **A `local` node is never a dependency edge**; a *definition's body* is walked for edges exactly
  as the root is.
- **Explanation output for a local is what a nested name produced**: `Spec.Build(body).Create(name)`
  or, with decoration, `Spec.Build(body).WhenTrue(t).WhenFalse(f).Create(name)`.
- **Existing documents with nested `name` / `whenTrue` / `whenFalse` keep parsing, binding and
  printing unchanged.** Nothing is removed from the schema, the parser or the printer.
- **`schemas/rule.v1.json` is extended in place** (see Task 0). It is the file both
  `RuleSchemaTests.cs` and `test/schema.test.ts` load.
- **Every accessible name the a11y and unit suites pin is kept or changed in the suite in the same
  task.** `pnpm --filter @motiv-rules/studio a11y` and `test` stay green; the conformance report is
  regenerated in the task that adds a surface.
- **Any change under `ui/packages/rules-react/src` edits the `<!-- react-adapter-price -->` table
  in `docs/adoption/index.md` in the same commit.** No task here should need one; if one does, the
  price table moves with it.
- **Dotnet invocations** on this machine: `env -u MallocStackLogging -u MallocNanoZone dotnet …`,
  outside the sandbox. Run `dotnet test src/Motiv.Serialization.Tests` for C# tasks and the full
  solution before the PR is marked ready.

## Deviations from the design doc, decided here

Recorded so the design doc can be amended in Task 0 rather than silently contradicted.

1. **No `$schema` version gate.** `RuleDocumentParser` accepts any string for `$schema` and never
   compares it; what rejects new documents on old readers is the unknown-property rule. Adding a
   version gate is new machinery with no consumer. The schema *file* is extended in place; the
   design doc's Compatibility section is rewritten to say so.
2. **A local is bound at each reference, not memoised per `(name, TModel)`.** Memoisation would
   thread a scope object through every method of four deliberately duplicated binders plus
   `CollectionBinding`. Specs are values: binding a definition twice yields two equal specs. The
   memo is an optimisation to add if measured. Cycle and unknown-local detection, which memoisation
   would have carried, move to a resolve pass in the parser (Task 6), where every other structural
   error already lives.
3. **`DocumentReferences` does change.** A definition's body references catalog propositions, and
   those are real dependency edges (async detection, approval gate, dependency graph). It walks
   definitions; it still never emits a `local` name.
4. **Locals do not open as tabs.** `DocKind` is a closed two-member union threaded through routing,
   listings, the tab strip and reference statuses. A definitions panel inside the document is the
   deliverable; a sub-document tab is not.
5. **`PayloadPopover` is a second nested-name editor** the design doc did not know about. Its Name
   field is retired for non-root paths alongside `DecorationEditor`'s.

## Path scheme for definitions

Both cores address a definition by path, so errors, spans and decoration-merging line up:

- the definition: `$.definitions.<name>`
- its body: `$.definitions.<name>.rule`, then the usual steps (`.and[0]`, `.not`, …)
- its decoration: `$.definitions.<name>.whenTrue` / `.whenFalse`

`<name>` follows the local-name grammar, which is why the TypeScript path tokenizer must admit
`-` and `_` in that one position.

---

## Tasks

### Task 0: Amend the design doc; extend the JSON schema

- [x] 
  `docs/superpowers/specs/2026-09-13-scoped-propositions-design.md`: rewrite *Compatibility* to
  match deviation 1; rewrite the *Binding semantics* paragraph on memoisation and the *Model type*
  paragraph to match deviation 2 ("bound at each reference against that reference's model");
  correct the `DocumentReferences` sentence under *Why a distinct `local` key* to match deviation 3;
  replace the "opens as a document in the dynamic-tabs shell" bullet with the definitions panel;
  add the `PayloadPopover` note. `schemas/rule.v1.json`: add envelope property
  `"definitions": { "type": "object", "propertyNames": { "pattern": "^[A-Za-z_][A-Za-z0-9_-]*$" }, "additionalProperties": { "$ref": "#/$defs/definition" } }`;
  add `$defs.definition` = object with required `rule` (`$ref: #/$defs/node`), optional `whenTrue`
  / `whenFalse` (`$ref: #/$defs/payload`), `additionalProperties: false`; add `$defs.localNode` =
  object with required `local` (`$ref: #/$defs/nonEmptyString`, plus the same pattern) and the
  optional decoration keys every node def carries, `additionalProperties: false`; add it to
  `$defs.node.oneOf`. Tests: `src/Motiv.Serialization.Tests/RuleSchemaTests.cs` — add to
  `ValidDocuments` a document with one definition and one `local` reference, and a definition with
  `whenTrue`/`whenFalse`; add to `InvalidDocuments` a `local` with a dotted name, a definition
  missing `rule`, a `definitions` value that is an array, and a definition with an unknown key.
  `ui/packages/rules-core/test/schema.test.ts` — the same four pairs through Ajv. Run both.
  Commit: `Schema — definitions and local nodes (#234)`.

### Task 1: Core model: `LocalNode`, `definitions`, `nodeKind`, name grammar

- [x] 
  `ui/packages/rules-core/src/document.ts`: `export interface LocalNode extends Decoration { local: string }`;
  add to `RuleNode`; `NodeKind` gains `'local'`; `KIND_ORDER` gains `'local'` after `'expression'`;
  `export function isLocalNode(node: RuleNode): node is LocalNode`;
  `export interface Definition { rule: RuleNode; whenTrue?: Payload; whenFalse?: Payload }`;
  `RuleDocument` gains `definitions?: Record<string, Definition>`. New file
  `ui/packages/rules-core/src/localNames.ts`:
  `export const LOCAL_NAME_PATTERN = /^[A-Za-z_][A-Za-z0-9_-]*$/;`
  `export function isValidLocalName(name: string): boolean;`
  `export function normalizeLocalName(typed: string): string` — the as-you-type rule: each space
  becomes `-`, each character outside `[A-Za-z0-9_-]` is dropped, a leading run of characters
  outside `[A-Za-z_]` is dropped (so the result is empty or valid-prefixed); **length-preserving
  for the space case only**, documented as such;
  `export function finishLocalName(typed: string): string` — the on-blur rule: `normalizeLocalName`,
  then runs of `-` collapse to one and trailing `-` are trimmed. `src/nodeSummary.ts`: `summarize`
  gains a branch before the spec fallback returning `{ badge: 'let', description: node.local, kind: 'spec' }`.
  `src/index.ts` exports `isLocalNode`, `LocalNode`, `Definition`, `LOCAL_NAME_PATTERN`,
  `isValidLocalName`, `normalizeLocalName`, `finishLocalName`; `test/api-surface.test.ts` adds
  the five value exports in alphabetical position. `src/contracts.ts` `RuleErrorCode` gains
  `'UnknownLocal'` and `'InvalidLocalName'` (C# mirrors in Task 6; `CycleDetected` already exists
  in both). Tests: `test/document.test.ts` — `nodeKind({ local: 'x' })` is `'local'`, `isLocalNode`
  true/false, `childPaths` of a local is `[]`; new `test/localNames.test.ts` — table of
  `normalizeLocalName` cases (`'is active'`→`'is-active'`, `'is  active'`→`'is--active'`,
  `'9x'`→`'x'`, `'a.b'`→`'ab'`, `''`→`''`) and `finishLocalName` cases (`'is--active-'`→
  `'is-active'`, `'---'`→`''`), and `isValidLocalName` for `'a-b_1'`, `'a.b'`, `'-a'`, `''`,
  `'a b'`. `test/nodeSummary.test.ts` — a local summarises with badge `let`. Commit:
  `Core — LocalNode, definitions and the local-name grammar (#234)`.

### Task 2: Core paths: definitions are addressable

- [x] 
  `ui/packages/rules-core/src/paths.ts`: `parseSteps` accepts a second root, `$.definitions.<name>`,
  where `<name>` matches `LOCAL_NAME_PATTERN`, followed by `.rule` and then ordinary steps; the
  step tokenizer keeps its `^([A-Za-z]+)(?:\[(\d+)\])?$` rule for ordinary steps and admits the
  name only in that position. `export const DEFINITIONS_ROOT = '$.definitions';`
  `export function definitionPath(name: string): string` → `$.definitions.<name>`;
  `export function definitionBodyPath(name: string): string` → `…/.rule`;
  `export function definitionNameOf(path: string): string | undefined` — the `<name>` when the
  path is under a definition. `getNode` / `setNode` resolve both roots (`setNode` on a definition
  body writes into `document.definitions[name].rule`, cloning as today). `listPaths` walks
  `document.rule` from `$.rule` **and every definition body from its `definitionBodyPath`**, in
  key order after the rule. Tests: `test/paths.test.ts` — `getNode` at `$.definitions.x.rule.and[1]`;
  `setNode` there returns a new document with the replacement and the rule untouched; `listPaths`
  includes definition bodies after the rule; `parseSteps` rejects `$.definitions.a.b.rule`,
  `$.definitions..rule`, `$.definitions.x` without `.rule` when used with `getNode` (a definition
  is not a node); `definitionNameOf('$.rule')` is undefined. Also `test/dsl-decorations.test.ts`
  — `mergeDecorations` carries a definition's `whenTrue` across a reparse when the definition of
  that name is still compatible, and drops it when the name is gone (this passes once `listPaths`
  covers definitions and `mergeDecorations` writes `whenTrue` onto the *definition* rather than
  its body node: `src/dsl/decorations.ts` gains that case, keyed on `definitionNameOf(path) &&
  path.endsWith('.rule')`). Commit: `Core — definitions are addressable paths (#234)`.

### Task 3: Core editor mutations: define, inline, rename, promote, decorate

- [x] 
  `ui/packages/rules-core/src/editor.ts`, each one `#commit`, each one undo step:
  `defineLocal(path: string, name: string): void` — moves the subtree at `path` into
  `definitions[name]` (carrying the subtree's own `name`/`whenTrue`/`whenFalse` onto the definition
  and stripping them from the body) and replaces the node at `path` with `{ local: name }`; throws
  on an invalid name or an existing definition of that name; `path` may not itself be under
  `$.definitions` of the same name (self-reference);
  `inlineLocal(path: string): void` — replaces the `local` node at `path` with a structured clone
  of its definition body, re-applying the definition's `whenTrue`/`whenFalse` and its name as the
  body's `name` (so inlining reproduces today's nested-name form exactly); if no other reference
  remains, the definition is removed;
  `renameLocal(from: string, to: string): void` — renames the key and every `local` reference in
  the rule and every definition; throws on invalid or taken `to`;
  `removeDefinition(name: string): void` — throws while any reference remains;
  `replaceLocalWithSpec(name: string, spec: string): void` — every `{ local: name }` becomes
  `{ spec }` and the definition is removed (the second half of Promote);
  `setDefinitionDecoration(name: string, decoration: Partial<Pick<Decoration, 'whenTrue' | 'whenFalse'>>): void`.
  Helper `export function localReferences(document: RuleDocument, name: string): string[]` (paths)
  in `src/paths.ts`, exported from the barrel and added to `APPROVED_API` alongside
  `definitionPath`, `definitionBodyPath` and `definitionNameOf` from Task 2 (add those three to
  the barrel and the snapshot in Task 2). `src/normalize.ts` `flatten`: a local is a leaf (verify by test; it already
  falls through). Tests: `test/editor.test.ts` — one `it` per method: define then undo restores the
  original document byte-for-byte; define moves decoration onto the definition; inline of the last
  reference removes the definition; inline of one of two references keeps it; rename rewrites a
  reference inside another definition; removeDefinition throws with a reference outstanding;
  replaceLocalWithSpec rewrites two references and drops the definition;
  setDefinitionDecoration clears a field when given `undefined`. Commit:
  `Core — local mutations on the editor store (#234)`.

### Task 4: DSL: `let` in lexer, parser and printer; bare-word resolution; round-trip

- [x] 
  `src/dsl/lexer.ts`: `DSL_KEYWORDS = ['param', 'let', 'in', 'as']`. `src/dsl/parser.ts`:
  `ParserState` gains `readonly locals = new Set<string>()`; a new `collectLocalNames(state)` runs
  before parsing and scans the token stream for `keyword let` followed by a `spec`-kind token at
  preamble depth (before the first token that is neither `param`-statement nor `let`-statement),
  adding each name; `parsePreamble(state)` replaces the direct `parseParameters` call: it loops
  while `peek().value` is `param` or `let`, delegating to `parseParameters` (unchanged body, one
  declaration per iteration now) or `parseLet`, and returns `{ parameters, definitions }`;
  `parseLet` consumes `let`, then `parseIdentifier(state, 'ExpectedLocalName', 'expected a name after `let`')`
  — if the name contains `.`, `state.error('DottedLocalName', 'a local name cannot contain a dot')`;
  if `!isValidLocalName(name)`, `state.error('InvalidLocalName', …)`; if already declared,
  `state.error('DuplicateLocal', …)`; then expects `equals` (`ExpectedEquals`), then
  `parseExpression(state, definitionBodyPath(name))`; the definition is stored with
  `Object.defineProperty` exactly as parameters are. `parsePrimary`'s spec branch: after reading
  the word, `if (state.locals.has(token.value)) { record span; return { local: token.value }; }`
  — arguments after a local are `UnexpectedArguments`. `parse()` returns `definitions` on the
  document when any were declared, in declaration order. Spans for a definition body are recorded
  under its `definitionBodyPath`. `src/dsl/printer.ts`: `printBody` gains
  `if (isLocalNode(node)) return node.local;` **before** the binary fall-through;
  `printDefinitions(definitions)` emits `let <name> = <printNode(body, '', 'block')>` one per
  definition, continuation lines indented by the block layout, joined with `\n`, followed by
  `\n\n`; `print` becomes `params + definitions + rule`. Tests: `test/dsl-lexer.test.ts` — `let`
  lexes as `keyword`; `test/dsl-parser.test.ts` — `let a = x && y\n\na & z` yields
  `{ definitions: { a: { rule: { andAlso: [...] } } }, rule: { and: [{ local: 'a' }, { spec: 'z' }] } }`;
  a `let` may reference a `let` declared *after* it; `param` before `let` works and `let` before
  `param` is `UnexpectedToken` at the `param`; a bare word not declared stays `{ spec }`;
  `test/dsl-parser-errors.test.ts` — one case each for `ExpectedLocalName`, `DottedLocalName`,
  `DuplicateLocal`, `ExpectedEquals`, `UnexpectedArguments` on a local; `test/dsl-printer.test.ts`
  — prints the preamble in `param`, `let`, rule order with blank-line separators, and a multi-line
  body indents its continuation; `test/dsl-roundtrip.test.ts` — add two `DOCUMENTS` entries (one
  definition referenced twice; a definition referencing another). `test/dsl-exports.test.ts` if it
  pins `DSL_KEYWORDS`. Commit: `DSL — let declarations and local references (#234)`.

### Task 5: DSL tooling: token runs, completion, diagnostics with severity, spans

- [x] 
  `src/dsl/tokenRuns.ts`: `TokenSpan.kind` gains `'local'`; `tokenSpans(text, locals?: ReadonlySet<string>)`
  marks a `spec` token whose value is in `locals` as `'local'`; new
  `export function declaredLocals(text: string): Set<string>` in `src/dsl/locals.ts` (regex scrape
  mirroring `PARAMETER_DECLARATION`: `\blet\s+([A-Za-z_][A-Za-z0-9_-]*)\s*=`), used by
  `tokenSpans` callers and completion. `src/dsl/completion.ts`: `CompletionItemKind` gains
  `'local'`; `localOptions(text)` offers each declared local with `detail: 'local'` and `boost: 1`
  above catalog specs. `src/dsl/types.ts`: `ParseResult` gains `warnings: DslError[]` (always
  present, possibly empty). `src/dsl/parser.ts`: after `collectLocalNames`, for each local whose
  name is also a catalog spec name (`options.catalog`), push
  `{ code: 'ShadowsCatalog', message: "shadows catalog proposition '<name>'" }` to `state.warnings`
  at the declaration's name token; and for each `as "…"` clause consumed by `parsePostfix` at a
  path other than `$.rule` push `{ code: 'PreferLet', message: 'prefer a `let` declaration' }`
  spanning the clause. `src/dsl/diagnostics.ts`: `RuleDiagnostic.severity` becomes
  `'error' | 'warning' | 'hint'`; `diagnosticsFor` maps `result.warnings` with severity `warning`
  for `ShadowsCatalog` and `hint` for `PreferLet`. Studio's `src/dsl/lint.ts` maps the three
  severities straight through (CodeMirror accepts all three). Tests: `test/token-runs.test.ts`
  (local run kind), `test/dsl-completion.test.ts` (declared local offered, boosted, not offered
  before its `let` is typed), `test/dsl-parser.test.ts` (warnings array, both codes, correct
  ranges; a root `as` produces no hint), `test/dsl-diagnostics.test.ts` (severity mapping);
  `ui/apps/studio/test/dsl/lint.test.ts` (a warning reaches CodeMirror as `warning`). Commit:
  `DSL — local token runs, completion and warnings (#234)`.

### Task 6: C# parser: `definitions`, `Local` operator, resolve pass, references, depth, comparer, parameters

- [x] `src/Motiv.Serialization/RuleOperator.cs`: add `Local`. `RuleNode.cs`: add
  `public string? LocalName { get; set; }` and `public RuleNode? Definition { get; set; }`
  (set by the resolve pass; the definition node carries `Name` = key and the definition's
  `WhenTrueText`/`WhenFalseText`/elements). `RuleDocument.cs`: add
  `IReadOnlyList<RuleNode> definitions` (5th ctor parameter, default `[]`), exposed as
  `Definitions`; the single `new RuleDocument(...)` site is the parser. `RuleErrorCode.cs`: add
  `UnknownLocal` and `InvalidLocalName` (doc comments in the file's style). `RuleDocumentParser.cs`:
  document switch gains `case "definitions": definitions = ParseDefinitions(property.Value, errors);`
  — an object whose every property is a definition; a non-object is `InvalidNode` at
  `$.definitions`; each key is validated with `SpecRegistry.IsValidName(key) && !key.Contains('.')`
  else `InvalidLocalName` at `$.definitions.<key>`; each value is an object with required `rule`
  (parsed via `ParseNode(value, $"$.definitions.{key}.rule", depth: 1, errors)`), optional
  `whenTrue`/`whenFalse` (through the existing `ParsePayloads` path, applied onto the body node),
  unknown keys `InvalidNode`; the body node's `Name` is set to the key. Node switch: `case "local":
  operators.Add(property)`; the "exactly one operator" message lists `local`; `ParseOperator`
  gains `case "local"` → `RuleNode(RuleOperator.Local, path) { LocalName = ReadNonEmptyString(...) }`,
  rejecting `args` (`UnexpectedArguments`) and `n`/`path` as the spec leaf does. **Resolve pass**
  (new `private static void ResolveLocals(RuleDocument document, List<RuleError> errors)` called
  after the envelope is parsed, before `ReportIfComposesTooDeeply`): walk the rule and every
  definition; for each `Local` node set `Definition` from the map or report `UnknownLocal` at the
  node's path; then detect cycles over the definition graph (DFS with an in-progress set; on a
  cycle report `CycleDetected` at `$.definitions.<name>` with the chain joined by ` → `, as
  `PropositionSet` does). `CompositionDepthOf` (the parser's source-less pre-filter) and
  `CompositionDepth.OfOperator`: `Local` → measure of `node.Definition` (already `Decorating()`
  because the body carries `Name`); a cycle is reported before either runs, so recursion is safe.
  `Propositions/DocumentReferences.cs`: `From` walks `document.Definitions` after `Root`; `Collect`
  ignores `Local` nodes. `RuleParameterSubstituter.Apply` is called by `RuleSerializer.Prepare`
  and `PrepareForValidation` on every definition body as well as `Root`.
  `Governance/RuleDocumentComparer.StructurallyEqual`: compares `Definitions` pairwise by key
  (`NodesEqual` on bodies) and `NodesEqual` compares `LocalName`. Tests, all in
  `src/Motiv.Serialization.Tests`: `RuleSerializerValidateTests.cs` — accepts a definition + local;
  `UnknownLocal` at the reference path; `InvalidLocalName` for `a.b` and `-a`; `CycleDetected` for
  `a → b → a` and for `a → a` with the chain in the message; `InvalidNode` for `definitions: []`,
  a definition without `rule`, an unknown definition key; `UnexpectedArguments` for a local with
  `args`; `Propositions/DocumentReferencesTests.cs` — a catalog spec referenced only inside a
  definition is an edge, a `local` name is not; `Governance/RuleDocumentComparerTests.cs` —
  renaming a definition key is a structural change, reordering keys is not;
  `RuleParameterTests.cs` — a `@param` inside a definition's `whenTrue` is substituted;
  `CompositionDepthTests.cs` — a local referencing a named definition counts one decorator.
  Commit: `Serialization — definitions, local nodes and the resolve pass (#234)`.

### Task 7: C# binders: bind a local through its definition

- [x] In each of `RuleBinder.cs`,
  `AsyncRuleBinder.cs`, `MetadataRuleBinder.cs`, `AsyncMetadataRuleBinder.cs`, the operator switch
  gains one arm: `RuleOperator.Local => BindNode<TModel>(node.Definition!, source, errors)` (the
  metadata binders' inline form; `Definition` is non-null after a clean parse, and `Bind` is never
  reached with errors). Because the definition node carries `Name` and the payload fields, the
  existing `Decorate` / inline tail produces `Spec.Build(body).Create(name)` or the WhenTrue form
  with no further code; a local's own `name`/`whenTrue` (if a document decorated the *reference*)
  applies on top, as for any node. `HigherOrder.cs` needs no change. Tests, one file per binder in
  the style of `AsyncRuleBinderTests.Should_wrap_a_name_only_node_like_a_fluent_create_wrapper`:
  a definition referenced once → `Reason == "<name> == true"`, inner `Assertions` preserved, and
  `ShouldBehaveIdentically` against the hand-built `Spec.Build(...).Create(name)`; a definition
  with `whenTrue`/`whenFalse` → `Reason` is the text; the same local referenced twice under `&`
  behaves identically to the hand-built composition with the same spec object used twice; a local
  inside a quantifier body binds against the element model (definition body references an
  `order` spec; the same definition referenced at the root reports `ModelTypeMismatch` at the
  reference inside the definition body path); the metadata binders' `TMetadata` variants with an
  object payload on the definition. Commit: `Serialization — binders resolve local references (#234)`.

### Task 8: Example writer and docs for the document format

- [x] 
  `src/examples/Motiv.RuleAuthoring.Blazor/Authoring/RuleDocumentWriter.cs`: emit `definitions`
  and `local` if its model can hold them; if its authoring model cannot produce them, add a test
  proving a document with definitions round-trips through *reading* unchanged and leave the writer
  alone, with a comment saying why. `docs/live-rules/RuleDocuments.md`: a *Definitions* section
  with the JSON example from the design doc, the name grammar, the path scheme, the error codes,
  and the sentence "a reader older than this version rejects the document with
  `unknown property 'definitions'`". The DSL page (find it: `grep -rl "param " docs/ | grep -i dsl`)
  gains `let`. `docs/toc.yml` / `docs/Overview.md` only if a page is added. Run the full solution
  suite here (`src/examples/*.Tests` included). Commit: `Docs — definitions in the document format and the DSL (#234)`.

### Task 9: Studio builder: local rows, retired nested decoration, read-only migration affordance

- [x] 
  `ui/apps/studio/src/builder/RuleNodeEditor.tsx`: `const isRoot = path === '$.rule'` (export
  `ROOT` from `BuilderPane.tsx`); the detail panel renders `<DecorationEditor>` only when
  `isRoot`; for a non-root node that carries `name`/`whenTrue`/`whenFalse`, render
  `<InlineDecorationNotice path={path} node={node} />` (new file
  `builder/InlineDecorationNotice.tsx`): a `.decoration-notice` block showing the name as
  `as "…"`, the payloads muted, and one button `Extract to definition` (aria-label
  `extract ${path}`) which calls `store.defineLocal(path, finishLocalName(node.name ?? summaryName))`
  where `summaryName` falls back to `local-<n>` for the first free `n`. A `local` node row:
  `summarize` already gives badge `let` and the name; `caretKind` is `'detail'`; its detail panel
  shows the definition body read-only as DSL (`printInline(definition.rule)`) with two buttons,
  `Inline` (`store.inlineLocal(path)`) and `Go to definition` (scrolls to the definitions panel
  row, Task 11). `dsl/PayloadPopover.tsx`: the Name field renders only when `path === '$.rule'`.
  `builder/NodeMenu.tsx`: new optional props `onExtractLocal?: () => void` and
  `onExtractCatalog?: () => void` adding items `Extract to definition` and `Extract to catalog…`
  (both offered on every non-root, non-local node); `onInline?: () => void` adding `Inline`
  (local nodes only). `RuleNodeEditor` wires `onExtractLocal` to open the name prompt of Task 10,
  `onExtractCatalog` to the dialog of Task 12, `onInline` to `store.inlineLocal`. Tests:
  `test/builder/RuleNodeEditor.test.tsx` — the nested `name at $.rule.and[0]` expectations become
  "no name field below the root"; the notice appears for a nested named node and Extract turns it
  into a local with the definition carrying the name; a local row shows `let` and its name; Inline
  restores the subtree with the name; `test/builder/NodeMenu.test.tsx` — the three new items
  appear where specified and not on the root; `test/dsl/PayloadPopover.test.tsx` — no Name field
  on a nested spec. Commit: `Studio — local rows, nested decoration retired, Extract and Inline (#234)`.

### Task 10: Studio: the normalising name input and the Extract-to-definition prompt

- [x] 
  New `ui/apps/studio/src/builder/LocalNameInput.tsx`:
  `props: { value: string; onChange(next: string): void; onCommit(): void; ariaLabel: string; taken: ReadonlySet<string> }`;
  an `<input className="control">` whose `onChange` applies `normalizeLocalName` and preserves the
  caret (`setSelectionRange(start, start)` after React commits, since the space case is
  length-preserving and a dropped character moves the caret left by one), whose `onBlur` applies
  `finishLocalName`, and which reports `taken.has(value)` and `!isValidLocalName(value)` as
  `aria-invalid` with a `.field-error` line. New `builder/ExtractLocalPrompt.tsx`: a small popover
  card (reuse `usePopoverCard`) titled `Extract to definition` with the input seeded from the
  node's existing `name` (finished) or the summary description, and `Create`/`Cancel`; on create
  calls `store.defineLocal(path, name)`. Tests: `test/builder/LocalNameInput.test.tsx` — typing
  `is active` yields `is-active` with the caret after the hyphen; typing `a.b` yields `ab`; blur
  collapses `a--b-` to `a-b`; a taken name is `aria-invalid`; `test/builder/ExtractLocalPrompt.test.tsx`
  — seeded, creates, cancels, Escape closes and restores focus to the menu trigger. Commit:
  `Studio — the normalising local-name input (#234)`.

### Task 11: Studio: the definitions panel

- [x] New `ui/apps/studio/src/panes/DefinitionsPane.tsx`,
  a `.pane` rendered inside `EditorPane` **above the builder** when the document has definitions
  (and always with an empty-state line when it has none: "No definitions. Extract a node to
  create one."). One row per definition: a `LocalNameInput` (rename via `store.renameLocal` on
  commit), the body as inline DSL (`NodeDsl`-style token spans with the `local` run kind), a
  reference count (`localReferences(...).length`), `When true` / `When false` inputs
  (`store.setDefinitionDecoration`), and a row menu with `Inline everywhere` (inline each
  reference, last one removes the definition), `Promote to catalog…` (Task 12) and `Remove`
  (enabled only at zero references). Row ids `definition-<name>` so `Go to definition` can
  `scrollIntoView` and focus. Tests: `test/panes/DefinitionsPane.test.tsx` — lists definitions in
  key order with counts; rename rewrites references; inline-everywhere removes the definition;
  Remove is disabled while referenced; decoration edits reach the definition. Commit:
  `Studio — the definitions panel (#234)`.

### Task 12: Studio: Extract to catalog and Promote

- [x] `ui/apps/studio/src/shell/WorkspaceShell.tsx`:
  `createFromDialog` accepts an optional `document: RuleDocument` in `DialogSeed` (new field
  `document?: RuleDocument`) and, when present, sends it instead of `{ rule: { spec: startsFrom } }`;
  the `PropositionDialog` hides the *starts from* select when a seed document is present. A new
  `onExtractToCatalog(path)` in the shell opens the dialog seeded with `{ rule: subtree }` (the
  node's own `name` moved to the document `name`, decoration kept on the root), title
  `Extract to catalog`; on success, `store.replaceNode(path, { spec: createdName })`. Promote from
  the definitions panel opens the same dialog seeded with `{ rule: definition.rule, name, whenTrue, whenFalse }`
  and on success calls `store.replaceLocalWithSpec(name, createdName)`. The created name is the
  dialog's value; the `PropositionDialog` name hint is unchanged (dots namespace). Tests:
  `test/shell/WorkspaceShell.test.tsx` (or the nearest existing shell test) — Extract to catalog
  posts the subtree as the document and replaces the node with a `spec`; Promote posts the
  definition and rewrites both references; a refused create leaves the document untouched and
  shows the error. Commit: `Studio — Extract to catalog and Promote (#234)`.

### Task 13: Studio DSL view: quick-fix for `PreferLet`, local highlighting

- [x] 
  `ui/apps/studio/src/dsl/lint.ts`: a diagnostic with code `PreferLet` gets
  `actions: [{ name: 'Extract to definition', apply(view, from, to) }]` which reads the node path
  from the diagnostic, calls `store.defineLocal(path, finishLocalName(name))`, and lets `dslSync`
  reformat the text from the tree (`sync.reformatFromTree()`). `src/dsl/motivLanguage.ts`: the
  stream parser marks a word in the document's declared locals (scraped with `declaredLocals` on
  each `startState`) with a `local` tag mapped to `var(--dsl-local)` in `theme.ts`/`tokens.css`
  (a new token drawn from an existing colour that already clears the ground; note the ratio in
  the commit). `builder/NodeDsl.tsx` passes `declaredLocals(print(document))` — or the definition
  keys directly — to `tokenSpans` so builder rows colour locals too; `.tok-local` in `app.css`.
  Tests: `test/dsl/lint.test.ts` — the action is attached to `PreferLet` only and applying it
  produces a `let`; `test/dsl/motivLanguage.test.ts` — a declared local tokenises as `local`;
  `test/builder/NodeDsl.test.tsx` — `.tok-local` present. Commit:
  `Studio — prefer-let quick-fix and local highlighting (#234)`.

### Task 14: Accessibility gate

- [x] `ui/apps/studio/e2e-a11y/stubs.ts`: `RULE_DOCUMENT` for the
  proposition fixture gains a definition and a local reference (the a11y builder scan then holds a
  local row). `e2e-a11y/axe.spec.ts`: `HARD_SURFACES` gains "the row menu on a nested node,
  offering Extract", "the extract-to-definition prompt", "the definitions panel, with a definition
  referenced twice", "a local row's detail panel"; `DANGER_SURFACES` gains "the definitions panel,
  refusing a taken name". Keyboard suite: one test that the definitions panel's row menu is
  reachable and Escape returns focus. `a11y/conformance.ts`: cite the new keyboard test where the
  record cites the row-menu test today. Run `pnpm --filter @motiv-rules/studio a11y` and
  `a11y:report`; commit the regenerated `docs/accessibility/vpat.md`. Commit:
  `Studio — a11y coverage for definitions (#234)`.

### Task 15: Wrap-up

- [x] Design doc status → `Implemented`, with the deviations section of this plan
  summarised under a *What changed in the build* heading. `README.md`: one bullet under the rules
  feature pointing at `let`. Run: `pnpm -C ui/packages/rules-core test typecheck build`,
  `pnpm --filter @motiv-rules/studio test typecheck a11y`, `pnpm -C ui verify:publishable`, the
  full .NET solution `dotnet test` (all target frameworks; report any framework that could not
  run), and `pnpm e2e` in Studio against the real host. Ledger #169: move the #234 row to the
  shipped table with the PR number. Commit: `Scoped propositions — design doc implemented (#234)`.

  **Note:** the ledger row (#169) moves on merge, not in this commit.

## Self-review against the spec

- *Two scopes, definitions block, `local` node* — Tasks 0, 1, 6.
- *Names: grammar, normalising input, validation as authority* — Tasks 1, 6, 10.
- *DSL: `let`, pre-collected names, bare-word resolution, shadowing warning, nested `as` hint and
  code action, decorations by definition path, completion and token runs* — Tasks 2, 4, 5, 13.
- *Binding semantics, model type, cycles, parameters* — Tasks 6, 7 (with deviation 2).
- *Compatibility* — Task 0 (deviation 1), Task 8.
- *Studio: retired nested fields, Extract with scope, Promote/Inline, definitions panel,
  migration affordance, a11y* — Tasks 9–14 (with deviations 4, 5).
- *Not in scope* — unchanged: no cross-document locals, no parameters on definitions, no object
  metadata on definitions (the C# parser accepts `whenTrue` objects on a definition because
  `ParsePayloads` does; the string binders reject them as they reject any object payload; the
  metadata binders bind them — Task 7 tests this rather than forbidding it).
