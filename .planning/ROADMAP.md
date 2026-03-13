# Roadmap: Motiv

## Milestones

- [x] **v1.0-rc1 Polish** - Phases 1-2 (complete), Phases 3-6 (deferred pending SyntaxFactory refactor)
- [ ] **CodeFix SyntaxFactory Refactor** - Phases 7-12 (in progress)

## Phases

<details>
<summary>v1.0-rc1 Polish (Phases 1-6) - Phases 1-2 complete, Phases 3-6 deferred</summary>

### Phase 1: Analyzer Expansion
**Goal**: Analyzer detects all boolean expression forms including `is` type-checks and pattern-matching expressions
**Depends on**: Nothing (first phase)
**Requirements**: ANLZ-01, ANLZ-02, ANLZ-03, ANLZ-04, TEST-01, TEST-02
**Success Criteria** (what must be TRUE):
  1. Analyzer detects simple type-check expressions (e.g., `obj is string`) and offers MOTIV0001 diagnostic
  2. Analyzer detects pattern-matching expressions (e.g., `obj is { Length: > 0 }`) and offers MOTIV0001 diagnostic
  3. Analyzer skips `is`/pattern expressions already nested inside detected binary expressions (no duplicate diagnostics)
  4. Analyzer skips `is`/pattern expressions inside `Spec.Build()` lambda bodies (no false positives in Motiv usage)
  5. Test suite covers all edge cases for `is` type-check and pattern-matching detection
**Plans**: 2 plans

Plans:
- [x] PLAN-01 -- TDD: Detect `is` type-check and pattern-matching expressions (ANLZ-01, ANLZ-02)
- [x] PLAN-02 -- TDD: Bidirectional nesting suppression and Spec.Build pattern suppression (ANLZ-03, ANLZ-04)

### Phase 2: CodeFix Foundation
**Goal**: CodeFix generates clean, context-aware proposition code with meaningful names derived from expression content
**Depends on**: Phase 1
**Requirements**: CFNM-01, CFNM-02, CFNM-03, CFCL-01, CFCL-02, CFCL-03, TEST-03, TEST-04
**Success Criteria** (what must be TRUE):
  1. Generated proposition class name derives from expression content (not hard-coded "Proposition")
  2. Generated model class name derives from expression content (not hard-coded "Model")
  3. Generated code contains no Debug.WriteLine calls
  4. Generated code does not add `using System.Diagnostics` unless already needed by other code
  5. Original expression preserved in clear, well-formatted comment
  6. Test suite verifies context-derived naming for various scenarios (method names, variable types)
  7. Test suite verifies absence of Debug.WriteLine in all generated outputs
**Plans**: 2 plans

Plans:
- [x] 02-01-PLAN.md -- TDD: Expression name derivation algorithm (CFNM-01, CFNM-02, CFNM-03)
- [x] 02-02-PLAN.md -- TDD: Integration, Debug.WriteLine removal, end-to-end tests (CFCL-01, CFCL-02, CFCL-03, TEST-03, TEST-04)

### Phase 3: CodeFix Simple Statements (DEFERRED)
**Status**: Deferred pending SyntaxFactory refactor (Phases 7-12)

### Phase 4: CodeFix Ternary Integration (DEFERRED)
**Status**: Deferred pending SyntaxFactory refactor (Phases 7-12)

### Phase 5: CodeFix Edge Cases (DEFERRED)
**Status**: Deferred pending SyntaxFactory refactor (Phases 7-12)

### Phase 6: XML Documentation Quality (DEFERRED)
**Status**: Deferred pending SyntaxFactory refactor (Phases 7-12)

</details>

### CodeFix SyntaxFactory Refactor (Phases 7-12)

**Milestone Goal:** Replace all string-based code generation in the CodeFix with pure SyntaxFactory API construction, ensuring generated code integrates properly with target codebases while preserving byte-identical output verified by existing tests.

**Phase Numbering:**
- Integer phases (7-12): Planned milestone work
- Decimal phases (e.g., 7.1): Urgent insertions if needed

#### Phase 7: SpecInvocation Migration
**Goal**: Spec invocation expressions are constructed via SyntaxFactory with a reliable trivia strategy established for all subsequent phases
**Depends on**: Phase 2 (CodeFix Foundation)
**Requirements**: SFMI-01, SFMI-02, SFMI-03
**Success Criteria** (what must be TRUE):
  1. `SpecInvocationSyntax.Create()` produces spec invocation expressions using SyntaxFactory API calls (no `ParseExpression` with interpolated strings)
  2. A CRLF trivia constant is defined and used consistently across all generated syntax nodes
  3. All existing spec invocation tests pass with identical output (no test changes)
**Plans**: 1 plan

Plans:
- [x] 07-01-PLAN.md -- Migrate SpecInvocationSyntax from ParseExpression to SyntaxFactory (SFMI-01, SFMI-02, SFMI-03)

#### Phase 8: Simple Spec Declaration Migration
**Goal**: Simple (single-clause) spec declarations are constructed entirely via SyntaxFactory, proving the primary constructor and fluent chain patterns
**Depends on**: Phase 7
**Requirements**: SFMD-01, SFMD-02, SFMD-03, SFMD-04
**Success Criteria** (what must be TRUE):
  1. `CustomSpecDeclarationSyntax.CreateInternal()` produces a class with primary constructor and `PrimaryConstructorBaseTypeSyntax` base type using SyntaxFactory (no StringBuilder or string interpolation for source code)
  2. Expression lambda bodies (e.g., `() => expr`) are constructed via SyntaxFactory (not string interpolation)
  3. Fluent method chains (`.WhenTrue().WhenFalse().Create()`) are constructed via nested `InvocationExpression`/`MemberAccessExpression`
  4. All existing simple spec declaration tests pass with identical output (no test changes)
**Plans**: 1 plan

Plans:
- [ ] 08-01-PLAN.md -- Migrate CreateInternal() from ParseCompilationUnit to SyntaxFactory (SFMD-01, SFMD-02, SFMD-03, SFMD-04)

#### Phase 9: Composed Spec Declaration Migration
**Goal**: Composed (multi-clause) spec declarations with block lambdas, local variables, and nested record types are constructed via SyntaxFactory
**Depends on**: Phase 8
**Requirements**: SFMC-01, SFMC-02, SFMC-03, SFMC-04, SFMC-05
**Success Criteria** (what must be TRUE):
  1. `CustomSpecDeclarationSyntax.CreateComposedInternal()` produces complete composed spec classes using SyntaxFactory (no StringBuilder or raw string code generation)
  2. Block lambdas with local variable declarations and return statements are constructed via SyntaxFactory
  3. Nested record declarations (e.g., `public record Model(...)`) are constructed via SyntaxFactory
  4. Clause name substitution in composition expressions uses `ReplaceNodes` on syntax trees (not `string.Replace` on source text)
  5. All existing composed spec tests pass with identical output (no test changes)
**Plans**: 2 plans

Plans:
- [ ] 09-01-PLAN.md -- Migrate composition pipeline from string to ExpressionSyntax (SFMC-01, SFMC-02, SFMC-04, SFMC-05)
- [ ] 09-02-PLAN.md -- Migrate nested record parameter list to SyntaxFactory (SFMC-03, SFMC-05)

#### Phase 10: Constructor Spec Declaration Migration
**Goal**: Constructor-based spec declarations with parameter injection and instance method references are constructed via SyntaxFactory
**Depends on**: Phase 9
**Requirements**: SFMK-01, SFMK-02, SFMK-03
**Success Criteria** (what must be TRUE):
  1. `CustomSpecDeclarationSyntax.CreateWithConstructorInternal()` produces constructor spec classes using SyntaxFactory (no StringBuilder or raw string code generation)
  2. Constructor parameters and instance method injection are expressed via SyntaxFactory node construction (not string manipulation)
  3. All existing constructor spec tests pass with identical output (no test changes)
**Plans**: 1 plan

Plans:
- [ ] 10-01-PLAN.md -- Verify constructor spec path is fully SyntaxFactory-migrated (SFMK-01, SFMK-02, SFMK-03)

#### Phase 11: Orchestrator Cleanup
**Goal**: The CodeFix orchestrator constructs fields, methods, and constructors directly via SyntaxFactory, eliminating the temporary-class-parse round-trip
**Depends on**: Phase 10
**Requirements**: SFMO-01, SFMO-02, SFMO-03
**Success Criteria** (what must be TRUE):
  1. `ConvertToSpecCodeFix.ReplaceMultiVariableExpression` constructs field declarations, method bodies, and constructor members directly via SyntaxFactory (no parsing a temporary class string to extract members)
  2. Comment trivia preserving original expressions is attached using SyntaxFactory trivia APIs
  3. All existing multi-variable tests pass with identical output (no test changes)
**Plans**: TBD

Plans:
(Plans will be created during `/gsd:plan-phase 11`)

#### Phase 12: Dead Code Removal
**Goal**: Obsoleted string manipulation utilities are removed, confirming the SyntaxFactory migration is complete and self-contained
**Depends on**: Phase 11
**Requirements**: SFMX-01, SFMX-02, SFMX-03
**Success Criteria** (what must be TRUE):
  1. Obsoleted string manipulation methods (`ToCamelCase` duplicate, `UpdateCompositionWithCamelCaseNames`, `ReplaceInstanceMethodCalls`) are removed from the codebase
  2. `EscapeDoubleQuotes()` is removed if `SyntaxFactory.Literal()` handles all string escaping (or retained with justification if still needed)
  3. The full test suite passes after all dead code removal (no regressions)
**Plans**: TBD

Plans:
(Plans will be created during `/gsd:plan-phase 12`)

## Progress

**Execution Order:**
Phases execute in numeric order: 7 -> 8 -> 9 -> 10 -> 11 -> 12

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Analyzer Expansion | v1.0-rc1 | 2/2 | Complete | 2026-02-06 |
| 2. CodeFix Foundation | v1.0-rc1 | 2/2 | Complete | 2026-02-08 |
| 3. CodeFix Simple Statements | v1.0-rc1 | 0/TBD | Deferred | - |
| 4. CodeFix Ternary Integration | v1.0-rc1 | 0/TBD | Deferred | - |
| 5. CodeFix Edge Cases | v1.0-rc1 | 0/TBD | Deferred | - |
| 6. XML Documentation Quality | v1.0-rc1 | 0/TBD | Deferred | - |
| 7. SpecInvocation Migration | SyntaxFactory | 1/1 | Complete | 2026-02-09 |
| 8. Simple Spec Declaration | 1/1 | Complete   | 2026-03-12 | - |
| 9. Composed Spec Declaration | 2/2 | Complete |  | - |
| 10. Constructor Spec Declaration | SyntaxFactory | 0/1 | Planned | - |
| 11. Orchestrator Cleanup | SyntaxFactory | 0/TBD | Not started | - |
| 12. Dead Code Removal | SyntaxFactory | 0/TBD | Not started | - |

---
*Roadmap created: 2026-02-06*
*Last updated: 2026-03-13 -- Phase 10 planned*
