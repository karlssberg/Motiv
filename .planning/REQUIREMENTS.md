# Requirements: Motiv

**Defined:** 2026-02-06
**Core Value:** Boolean expressions must produce meaningful, structured explanations — not just true/false.

## v1.0-rc1 Requirements

Requirements for release candidate. Each maps to roadmap phases.

### Analyzer Expansion

- [ ] **ANLZ-01**: Analyzer detects `is` type-check expressions (e.g. `obj is string`)
- [ ] **ANLZ-02**: Analyzer detects `is` pattern-matching expressions (e.g. `obj is { Length: > 0 }`)
- [ ] **ANLZ-03**: Analyzer correctly skips `is`/pattern expressions nested inside already-detected binary expressions
- [ ] **ANLZ-04**: Analyzer correctly skips `is`/pattern expressions inside `Spec.Build()` lambdas

### CodeFix — Name Derivation

- [ ] **CFNM-01**: CodeFix derives proposition class name from the containing method or enclosing context (not hard-coded "Proposition")
- [ ] **CFNM-02**: CodeFix derives model class name from parameter type or enclosing context (not hard-coded "Model")
- [ ] **CFNM-03**: Single-variable path uses actual variable type name for model (already partially works — verify and test)

### CodeFix — Clean Output

- [ ] **CFCL-01**: Generated code does not contain `Debug.WriteLine` calls
- [ ] **CFCL-02**: Generated code does not add `using System.Diagnostics` when not otherwise needed
- [ ] **CFCL-03**: Generated comment showing original expression is clear and well-formatted

### CodeFix — Statement Contexts

- [ ] **CFSC-01**: CodeFix handles `if` statement conditions — replaces condition with spec invocation
- [ ] **CFSC-02**: CodeFix handles `while` loop conditions — replaces condition with spec invocation
- [ ] **CFSC-03**: CodeFix handles `do-while` loop conditions — replaces condition with spec invocation
- [ ] **CFSC-04**: CodeFix handles ternary/conditional expressions — replaces condition, uses true/false branches for `WhenTrue`/`WhenFalse` fluent methods

### Test Coverage

- [ ] **TEST-01**: Analyzer tests cover `is` type-check detection and edge cases
- [ ] **TEST-02**: Analyzer tests cover `is` pattern-matching detection and edge cases
- [ ] **TEST-03**: CodeFix tests cover context-derived naming for proposition and model
- [ ] **TEST-04**: CodeFix tests cover absence of Debug.WriteLine in generated output
- [ ] **TEST-05**: CodeFix tests cover `if` condition context
- [ ] **TEST-06**: CodeFix tests cover `while`/`do-while` condition contexts
- [ ] **TEST-07**: CodeFix tests cover ternary expression with WhenTrue/WhenFalse integration
- [ ] **TEST-08**: CodeFix tests cover edge cases (nested conditions, complex patterns, multiple variables)

### XML Documentation Quality

- [ ] **XDOC-01**: Review and improve XML doc quality on core library public APIs (SpecBase, Spec, Policy, BooleanResultBase)
- [ ] **XDOC-02**: Review and improve XML doc quality on expression tree public APIs (ExpressionTreeExtensions)
- [ ] **XDOC-03**: Ensure parameter descriptions and return value docs are accurate and helpful

## Future Requirements

Deferred beyond v1.0-rc1.

- **ANLZ-F01**: Analyzer detects method calls returning bool (e.g. `list.Contains(x)`, `string.IsNullOrEmpty(s)`)
- **ANLZ-F02**: Analyzer detects standalone negation of boolean identifiers (e.g. `!isValid`)
- **CFSC-F01**: CodeFix handles LINQ `.Where()` lambda predicates
- **CFSC-F02**: CodeFix handles `switch` expression arms with boolean conditions

## Out of Scope

| Feature | Reason |
|---------|--------|
| Full IDE integration beyond Roslyn | Too broad for RC — analyzer/codefix is sufficient |
| Performance optimization | No evidence of performance issues |
| NuGet branding/metadata polish | Cosmetic — post-RC |
| Analyzer severity configuration | Info severity is appropriate for suggestions |
| CodeFix "undo" or preview refinements | Standard Roslyn preview is adequate |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| ANLZ-01 | Phase 1 | Pending |
| ANLZ-02 | Phase 1 | Pending |
| ANLZ-03 | Phase 1 | Pending |
| ANLZ-04 | Phase 1 | Pending |
| TEST-01 | Phase 1 | Pending |
| TEST-02 | Phase 1 | Pending |
| CFNM-01 | Phase 2 | Pending |
| CFNM-02 | Phase 2 | Pending |
| CFNM-03 | Phase 2 | Pending |
| CFCL-01 | Phase 2 | Pending |
| CFCL-02 | Phase 2 | Pending |
| CFCL-03 | Phase 2 | Pending |
| TEST-03 | Phase 2 | Pending |
| TEST-04 | Phase 2 | Pending |
| CFSC-01 | Phase 3 | Pending |
| CFSC-02 | Phase 3 | Pending |
| CFSC-03 | Phase 3 | Pending |
| TEST-05 | Phase 3 | Pending |
| TEST-06 | Phase 3 | Pending |
| CFSC-04 | Phase 4 | Pending |
| TEST-07 | Phase 4 | Pending |
| TEST-08 | Phase 5 | Pending |
| XDOC-01 | Phase 6 | Pending |
| XDOC-02 | Phase 6 | Pending |
| XDOC-03 | Phase 6 | Pending |

**Coverage:**
- v1.0-rc1 requirements: 25 total
- Mapped to phases: 25
- Unmapped: 0

**Coverage: 100%** — All requirements mapped to phases.

---
*Requirements defined: 2026-02-06*
*Last updated: 2026-02-06 after roadmap creation*
