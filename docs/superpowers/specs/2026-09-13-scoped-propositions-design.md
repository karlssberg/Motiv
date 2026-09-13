# Scoped propositions: document-local definitions

**Date:** 2026-09-13
**Status:** Proposed
**Ledger:** child of #169
**Primary source:** the Studio observation below, and the binder as it stands at `345d1b02`.

## Problem

Studio lets an author type a **Name** (and a When true / When false) on any nested node of a rule.
The screenshot that prompted this doc showed the field at the indent of the node's *children*,
so it read as belonging to `customer.is-active` when it belonged to the `andAlso` above it. That is
a one-line layout defect (`.node-detail` indents 32px, a child level 28px) and is fixed on its own.

The defect exposed the real question: **what does a name on a nested node mean?** The binder
answers it in `RuleBinder.Decorate`:

```csharp
return node.Name is null ? spec : Spec.Build(spec).Create(node.Name);
```

A nested name is not a label. It wraps the subtree in a minimal proposition, so the node's `Reason`
and `Assertions` become `"is active and is adult == true"` and the children's assertions retreat to
`Underlying`. The author has defined a proposition and inlined it, anonymously. Everything a
proposition would give them is missing: an identity, reuse (even within the same rule), a place in
the dependents graph, a version. And the document schema now has two spellings of one thing — an
inline `name` on a subtree, and a `spec` reference to a stored proposition — that bind to
structurally identical results.

The first instinct is "drop inline naming, offer *Extract to proposition* instead". That is right
about the semantics and wrong about the cost: today the only destination for an extracted
proposition is the **catalog**. Extraction is a server round-trip, a namespace decision, and a new
entry others can depend on. For a grouping the author considers private to one rule, that is the
wrong weight, and the inline name exists precisely because it is cheap.

The resolution is to give propositions a **scope**.

## Decision

Two scopes, with the third the user proposed falling out of the second:

| Scope | Where it lives | Who can reference it | Identity |
|---|---|---|---|
| **Catalog** | The proposition store, as today | Any document in the store's model scope | Dotted name, e.g. `customer.is-active` |
| **Document-local** | A `definitions` block on the document that uses it | That document only | One catalog-shaped segment, e.g. `is-active-and-adult` |

A **proposition is itself a document** (`PropositionCreateRequest.document` is a `RuleDocument`),
so "proposition-scoped" is a document-local definition inside a proposition. No third mechanism.

Document-local definitions are **lexical**: the `let` / `where` block of a rule. Locals travel
with the document, so versioning, import/export, the CAS save, and the dependents list stay
coherent with no store change. The blast radius of a local is its own document by construction.

**Inline `name` / `whenTrue` / `whenFalse` on nested nodes are retired from the authoring
surface.** They remain in the schema and binder for existing documents; Studio stops writing them
and offers a one-click migration (below). The root's decoration is untouched: `name` at the root is
the rule's own name.

## Document shape

```json
{
  "name": "qualifies for loyalty discount",
  "definitions": {
    "is-active-and-adult": {
      "rule": { "andAlso": [ { "spec": "customer.is-active" }, { "spec": "customer.is-adult" } ] },
      "whenTrue": "customer is active and an adult",
      "whenFalse": "customer is inactive or a minor"
    }
  },
  "rule": {
    "and": [
      { "local": "is-active-and-adult" },
      { "spec": "customer.has-orders" }
    ]
  }
}
```

- `definitions` is a map from local name to a **definition**: a `rule` subtree plus optional
  `whenTrue` / `whenFalse` decoration. The key is the name; the definition carries no `name` of
  its own, and no model type — see *Model type* under binding.
- A local is referenced by a **new node kind**, `{ "local": "<name>" }`, not by `spec`. This is the
  design's one contentious choice and is argued below.
- A definition may reference other locals and any catalog proposition. Locals may not be
  recursive, directly or through other locals.
- `definitions` on a proposition document works identically.

### Names

A local name follows **the same grammar as a catalog name segment**: the DSL's spec-word class
(`WORD_START_CHARS` / `WORD_REST_CHARS` in the lexer — a letter or underscore, then letters,
digits, hyphens, underscores) and `SpecRegistry.IsValidName` on the C# side, minus the dot,
which is the catalog's namespace separator and has no meaning inside one document. Spaces are
not admitted. Three reasons, in order of weight:

1. **The DSL.** A local is declared with `let <name> = …` and referenced as a bare word, so
   the name must be a word the lexer already reads as one: `is active and is adult` is four
   tokens, two of them keywords. Quoting would make locals the one reference that reads
   differently from every other name in the language.
2. **Promotion is a rename, not a rewrite.** A local that already satisfies the segment grammar
   promotes to the catalog by prefixing a namespace. A name that needed transformation on the
   way out would make Promote change explanation text, since the name *is* the assertion.
3. **Explanation output is already in this shape.** `customer.is-active == true` is what a
   catalog reference asserts today; `is-active-and-adult == true` is the same convention. Prose
   belongs in `whenTrue` / `whenFalse`, which is what they are for.

**Studio normalises as the author types.** The name input never holds an invalid name:

- a typed space becomes `-` (length-preserving, so the caret does not jump in a controlled
  input);
- a character outside the grammar is dropped;
- on blur, runs of `-` collapse to one and trailing `-` are trimmed. Leading `-` cannot occur,
  since the grammar's first character is a letter or underscore;
- case is preserved. The grammar admits it and the catalog does not force lower-case, so neither
  do locals.

Validation is still the authority. The input is a convenience; the DSL, a JSON edit and an
imported document all bypass it, so an invalid local name is a validation error in core and a
bind error in C#, exactly as an invalid catalog name is today.

### The DSL: `let` declarations

The DSL already has a preamble: a run of `param name: type = default` statements, a blank line,
then the expression. A local is one more kind of preamble statement, with the same shape:

```
param minOrders: integer = 3

let is-active-and-adult = customer.is-active && customer.is-adult

is-active-and-adult & customer.has-orders(min = @minOrders)
```

- **`let` joins `param` as a keyword.** Deliberated and settled on 2026-09-13. `const` was
  rejected because its meaning is "not reassignable", and nothing in the DSL is ever assigned
  twice, so the word contrasts with nothing. `spec` was rejected because it is already the JSON
  key for a *catalog* reference, and using it to declare a *local* would invert its meaning
  across the two surfaces. `def` is a verb where `param` set a noun style, and reads as a
  function. `local` was the runner-up — it names the scope and matches the JSON key — and lost
  only on familiarity: `let name = expr` is the binding form every expression-language reader
  already knows, and it invites no "where is `global`?" question. `param` statements come
  first, since a `let` body may use `@param` references; then the `let` block; then the
  expression. The printer emits the three
  in that order, blank-line separated, and a `let` whose body breaks across lines indents its
  continuation exactly as a block-layout expression does.
- **The identifier follows the local-name grammar** above. `let a.b = …` is a parse error
  naming the dot, since a dotted name would be indistinguishable from a catalog reference.
- **A bare word resolves against the document's own declarations.** The parser collects the
  `let` names before it parses any body, so declaration order does not matter and a `let` may
  reference another declared later — the cycle check, not the text order, is what forbids
  recursion. This is deliberate: the stored JSON is a map with no order, and the printer must be
  able to emit any document without first topologically sorting it. A word that names a declared
  `let` becomes `{ "local": … }`; any other word becomes `{ "spec": … }` as today.
- **Shadowing is a diagnostic, not a rule.** A local cannot collide with a namespaced catalog
  name (it has no dot), but a root-level catalog proposition — `customer` may be both a namespace
  and a proposition — can share a name with a `let`. The `let` wins inside its document, and
  the parser, which already carries the catalog for argument ordering, reports
  `shadows catalog proposition 'customer'` as a warning at the declaration.
- **`as "name"` on a nested node is the retired feature.** The parser keeps accepting it and the
  printer keeps emitting it, so an existing document round-trips unchanged; completion stops
  offering it below the root, and diagnostics attach a hint, *"prefer `let`"*, with a code
  action that lifts the named subtree into a declaration. `as` on the root expression is the
  rule's own name and is untouched.
- **`whenTrue` / `whenFalse` stay out of the text**, as they are for every node today, and ride
  the same side channel: `mergeDecorations` carries a definition's payloads across a reparse by
  the definition's path (`definitions.<name>`). Renaming a `let` therefore drops its payloads,
  exactly as retargeting a `spec` node drops that node's payloads now.
- **Completion** offers declared `let` names alongside `@param` references, and `tokenRuns`
  gains a `local` run kind so the editor can colour a local reference apart from a catalog one.

This replaces the earlier idea of a sigil-prefixed reference. A sigil would have made keyword
collision impossible, but `let` makes it a parse error at the declaration instead, which is
where an author expects to be told.

### Why a distinct `local` key rather than local-first resolution of `spec`

Reusing `spec` with local-first lookup is fewer moving parts, and was rejected for three reasons:

1. **Shadowing.** A local named `customer.is-active` would silently hijack every reference in the
   document. A validation error on collision is possible, but then a *catalog* publish elsewhere
   can break a document that was valid when saved — the failure surfaces in the wrong place.
2. **The dependents index.** `DocumentReferences.From` collects `spec` names as the document's
   outgoing edges. With local-first `spec`, it would have to subtract the definitions map before
   every read. With `local`, `DocumentReferences` still changes shape — it now walks
   `definitions` too, because a definition's body can reference catalog propositions, and those
   are real dependency edges the index must not miss — but it never emits a `local` name as an
   edge. A local is not a dependency; the catalog propositions it reaches are.
3. **The DSL resolves, the JSON records.** In the DSL a local *is* a bare word, resolved against
   the document's own `let` declarations at parse time (below). That is acceptable there because
   the declaration is in the same text, a screen above. The JSON is what is stored, bound and
   diffed, and it should not need a resolution pass to say what a node means: the parser writes
   `local` or `spec` and the binder reads it.

The cost is one more node kind through schema, parser, binder, `nodeKind`, DSL lexer/parser/printer,
diagnostics, completion and the editor. That is the honest size of the feature under either option;
the distinct key makes it explicit rather than hiding it in resolution rules.

### Binding semantics

A local binds exactly as a nested name does today, so **explanation output is unchanged**:

```csharp
// definition with no decoration
Spec.Build(subtree).Create(name)
// definition with whenTrue/whenFalse
Spec.Build(subtree).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(name)
```

A local is bound **at each reference**, against that reference's model type. Specs are values, so
binding the same definition twice yields two equal specs, not two distinguishable objects — there
is nothing to share and nothing a caller could observe by comparing them. Binding twice is an
optimisation opportunity, not a correctness requirement; memoisation can be added later if it is
measured to matter, but it is not part of this slice.

**Model type.** A definition declares none. It is bound **against the model type of the reference
that names it**: a local referenced at the document root binds against the document's model, and
the same local referenced inside a quantifier body binds again against the body's element model.
A definition whose catalog references do not fit the use-site model fails at that reference with
the existing check (`RuleBinder`, "has model type X but the document is being loaded for Y"), so
nothing new is reported and nothing new is declared. The DSL has no place to write a model type,
which is the other reason not to have one.

**Cycles and depth.** Cycle detection and unknown-local detection are a **resolve pass** in the
C# parser, run before binding: it walks `definitions`, following `local` references, and reports
an unknown name or a cycle (`a` → `b` → `a`, or `a` → `a`) as a bind error with a new
`RuleErrorCode`, at the reference that names the unknown local or closes the cycle. Locals are
folded into the existing `CompositionDepth.ReportIfTooDeep` measure so that a deep local counts
toward `MaxCompositionDepth` exactly as a deep catalog reference does.

**Parameters.** A definition may use the document's `@parameter`s; they are substituted by
`RuleParameterSubstituter` before binding exactly as in the root rule. Definitions do not declare
parameters of their own — that is what promotion to the catalog is for.

### Compatibility

There is no `$schema` version gate: `RuleDocumentParser` accepts any string for `$schema` and
never compares it, and this stays true after this change. What makes a document carrying
`definitions` or a `local` node **unreadable by a reader that predates this change** is the same
rule that already governs every other key: `RuleDocumentParser` **rejects unknown document-level
properties** (`unknown property 'definitions'`), and so does its handling of node keys. A reader
built before this slice has never heard of `definitions` or `local` and refuses the document on
that ground alone, exactly as it would refuse a typo'd key today.

- The schema *file*, `schemas/rule.v1.json`, is extended in place — the new keys are additive to
  the file, even though a document that uses them is not additive from an old reader's point of
  view.
- Studio and the `@motiv-rules/core` schema validator gain the keys in the same release.
- The EF and JSON stores need no migration: documents are opaque JSON to them.

Existing documents with inline nested `name` / `whenTrue` / `whenFalse` **remain valid and bind
as before**. Nothing is removed from the binder.

## Studio

- **The details panel** of a nested node no longer shows Name / When true / When false. The root
  keeps them. The panel indent is corrected so it sits at the row's own level. `PayloadPopover`,
  the DSL view's payload editor, is a second place that edits a node's name and payloads by
  nested path; its Name field is retired for non-root paths alongside `DecorationEditor`'s, for
  the same reason.
- **Extract** on any nested composition or leaf offers a scope:
  - **This document** (default): asks for a local name, moves the subtree into `definitions`, and
    replaces it with `{ "local": … }`. No server round-trip; an undo step like any other.
  - **Catalog**: the existing proposition-create workflow (namespace + name + description), then
    the subtree is replaced with `{ "spec": … }`.
- **Promote** on a local definition runs the catalog path and rewrites every `local` reference
  to `spec`. **Inline** on either kind of reference replaces it with the definition's subtree
  (for a catalog reference, a copy of the proposition's current document — the reference is gone
  and the dependents edge with it, which the confirm says).
- **A definitions panel** per document lists locals with reference counts and is where a local is
  read and edited. Locals do not open as tabs in the dynamic-tabs shell (#233): a definition is
  part of its document, not a document of its own, so the panel edits the same `RuleEditorStore`
  at a sub-path in place, and the parent tab's draft and undo are shared, not forked.
- **Migration of an existing inline name**: a nested node that carries `name` / `whenTrue` /
  `whenFalse` shows the decoration read-only on the row with a single **Extract** affordance. One
  click converts it into a local of that name. Studio never writes a nested decoration again.
- **DSL surface**: the `let` preamble as specified above. Extract-to-local in the builder and
  the *prefer `let`* code action in the DSL view produce the same document, and `dslSync` keeps
  the two views agreeing as it does for every other edit.
- **Accessibility**: the definitions panel and the scope choice follow the conventions in
  `docs/accessibility/index.md`; `accessibleExpression` for a `local` node is its name, as for a
  `spec` node.

## Not in scope

- **Cross-document locals** (a rule reading another rule's definitions). That is the catalog.
- **Parameters on definitions.** Promote to the catalog to parameterise.
- **Metadata (`Payload` objects) on definitions.** Strings only, as for the root decoration
  today; the metadata binders follow when the string path is proven.
- **Removing nested decoration from the schema.** It stays readable; only authoring is retired.

## Work list

Each item is TDD, and the plan is written in the same commit as the implementation (per the slice
rule in `CLAUDE.md`).

1. **Core model** (`@motiv-rules/core`): `LocalNode`, `definitions` on `RuleDocument`, `nodeKind`,
   `isLocalNode`, schema validator, `normalize`, `paths` (definitions are addressable paths),
   mutations `defineLocal` / `inlineLocal` / `promoteLocal`, `normalizeLocalName` (the
   as-you-type and on-blur rules above, exported so the editor and tests share one definition),
   validation for invalid name, unknown local, cycle, and unused definition (a warning).
   API-surface snapshot updated.
2. **C# parser and binder** (`Motiv.Serialization`): `RuleDocument.Definitions`,
   `RuleNode` local kind, `Decorate`-equivalent bind at each reference against that reference's
   model type, a resolve pass for unknown-local and cycle detection with a new error code,
   `CompositionDepth` measuring through locals, `DocumentReferences` walking `definitions` for
   catalog edges while still never emitting a `local` name. Sync, async and both metadata binders.
3. **DSL**: `let` keyword; preamble parse with pre-collected declarations; bare-word
   resolution to `local` / `spec`; dotted-identifier and duplicate-declaration errors; shadowing
   warning; *prefer `let`* hint and code action for nested `as`; printer with `param` /
   `let` / expression blocks; `definitions.<name>` paths through `mergeDecorations`;
   completion and `tokenRuns`; round-trip tests in both directions, including a document with an
   existing nested `as`.
4. **Studio**: panel indent fix; nested decoration retired from authoring; Extract with scope
   and the normalising name input; Promote / Inline; definitions panel; migration affordance
   (an existing inline name is normalised on the way in, and the author sees the result before
   confirming); a11y sweep and conformance report regenerated.
5. **Docs**: `docs/serialization/` schema page for `definitions` and `local`; Studio docs; this
   design doc's status flipped to Implemented in the landing commit.
