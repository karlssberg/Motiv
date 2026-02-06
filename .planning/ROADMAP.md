# Roadmap: Motiv v1.0-rc1 Polish

## Overview

This roadmap takes the existing analyzer and codefix from functional to release-candidate quality. We expand analyzer detection to pattern-matching, improve codefix output quality (context-derived names, clean code), add support for control flow statement contexts (if/while/ternary), and polish public API documentation. Each phase delivers complete, tested capabilities.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Analyzer Expansion** - Detect `is` type-checks and pattern-matching expressions
- [ ] **Phase 2: CodeFix Foundation** - Derive names from context and eliminate Debug.WriteLine
- [ ] **Phase 3: CodeFix Simple Statements** - Handle if/while/do-while condition contexts
- [ ] **Phase 4: CodeFix Ternary Integration** - Map ternary branches to WhenTrue/WhenFalse fluent methods
- [ ] **Phase 5: CodeFix Edge Cases** - Comprehensive test coverage for complex scenarios
- [ ] **Phase 6: XML Documentation Quality** - Review and improve public API documentation

## Phase Details

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
- [ ] PLAN-01 — TDD: Detect `is` type-check and pattern-matching expressions (ANLZ-01, ANLZ-02)
- [ ] PLAN-02 — TDD: Bidirectional nesting suppression and Spec.Build pattern suppression (ANLZ-03, ANLZ-04)

### Phase 2: CodeFix Foundation
**Goal**: CodeFix generates clean, context-aware proposition code with meaningful names
**Depends on**: Phase 1
**Requirements**: CFNM-01, CFNM-02, CFNM-03, CFCL-01, CFCL-02, CFCL-03, TEST-03, TEST-04
**Success Criteria** (what must be TRUE):
  1. Generated proposition class name derives from containing method or context (not hard-coded "Proposition")
  2. Generated model class name derives from parameter type or context (not hard-coded "Model")
  3. Generated code contains no Debug.WriteLine calls
  4. Generated code does not add `using System.Diagnostics` unless already needed by other code
  5. Original expression preserved in clear, well-formatted comment
  6. Test suite verifies context-derived naming for various scenarios (method names, variable types)
  7. Test suite verifies absence of Debug.WriteLine in all generated outputs
**Plans**: TBD

Plans:
(Plans will be created during `/gsd:plan-phase 2`)

### Phase 3: CodeFix Simple Statements
**Goal**: CodeFix handles if, while, and do-while condition contexts
**Depends on**: Phase 2
**Requirements**: CFSC-01, CFSC-02, CFSC-03, TEST-05, TEST-06
**Success Criteria** (what must be TRUE):
  1. CodeFix detects boolean expressions in `if` statement conditions and replaces with spec invocation
  2. CodeFix detects boolean expressions in `while` loop conditions and replaces with spec invocation
  3. CodeFix detects boolean expressions in `do-while` loop conditions and replaces with spec invocation
  4. Generated spec invocations work correctly in all statement contexts (compile and run)
  5. Test suite covers if, while, and do-while condition replacements
**Plans**: TBD

Plans:
(Plans will be created during `/gsd:plan-phase 3`)

### Phase 4: CodeFix Ternary Integration
**Goal**: CodeFix handles ternary/conditional expressions with WhenTrue/WhenFalse fluent methods
**Depends on**: Phase 3
**Requirements**: CFSC-04, TEST-07
**Success Criteria** (what must be TRUE):
  1. CodeFix detects boolean expressions in ternary/conditional operator conditions
  2. Generated spec uses `.WhenTrue(...)` with the true-branch expression
  3. Generated spec uses `.WhenFalse(...)` with the false-branch expression
  4. Generated spec invocation returns the appropriate branch value based on condition
  5. Test suite covers ternary expression conversion including nested and complex expressions
**Plans**: TBD

Plans:
(Plans will be created during `/gsd:plan-phase 4`)

### Phase 5: CodeFix Edge Cases
**Goal**: CodeFix handles complex, nested, and multi-variable edge cases reliably
**Depends on**: Phase 4
**Requirements**: TEST-08
**Success Criteria** (what must be TRUE):
  1. CodeFix correctly handles nested control flow statements (if inside while, etc.)
  2. CodeFix correctly handles complex boolean expressions with multiple variables
  3. CodeFix correctly handles expressions with mixed operators and parentheses
  4. CodeFix correctly handles edge cases like null-conditional operators combined with boolean logic
  5. Test suite exercises all identified edge cases and verifies correct code generation
**Plans**: TBD

Plans:
(Plans will be created during `/gsd:plan-phase 5`)

### Phase 6: XML Documentation Quality
**Goal**: Public API documentation is accurate, complete, and helpful
**Depends on**: Phase 5
**Requirements**: XDOC-01, XDOC-02, XDOC-03
**Success Criteria** (what must be TRUE):
  1. Core library public APIs (SpecBase, Spec, Policy, BooleanResultBase) have clear, accurate XML docs
  2. Expression tree public APIs (ExpressionTreeExtensions) have clear, accurate XML docs
  3. All parameter descriptions are present and meaningful
  4. All return value descriptions are present and meaningful
  5. Code examples in docs (if present) compile and demonstrate correct usage
**Plans**: TBD

Plans:
(Plans will be created during `/gsd:plan-phase 6`)

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Analyzer Expansion | 0/2 | Planning complete | - |
| 2. CodeFix Foundation | 0/TBD | Not started | - |
| 3. CodeFix Simple Statements | 0/TBD | Not started | - |
| 4. CodeFix Ternary Integration | 0/TBD | Not started | - |
| 5. CodeFix Edge Cases | 0/TBD | Not started | - |
| 6. XML Documentation Quality | 0/TBD | Not started | - |

---
*Roadmap created: 2026-02-06*
*Last updated: 2026-02-06*
