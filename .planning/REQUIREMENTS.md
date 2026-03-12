# Requirements: Motiv CodeFix SyntaxFactory Refactor

**Defined:** 2026-02-08
**Core Value:** Boolean expressions must produce meaningful, structured explanations — not just true/false.

## Refactor Requirements

Requirements for SyntaxFactory migration. Each maps to roadmap phases.

### SpecInvocation Migration

- [x] **SFMI-01**: `SpecInvocationSyntax.Create()` uses SyntaxFactory instead of `ParseExpression($$"""...""")`
- [x] **SFMI-02**: CRLF trivia constant established and used consistently across all migration targets
- [x] **SFMI-03**: All existing spec invocation tests pass unchanged

### Simple Spec Declaration Migration

- [ ] **SFMD-01**: `CustomSpecDeclarationSyntax.CreateInternal()` uses SyntaxFactory with `PrimaryConstructorBaseTypeSyntax`
- [ ] **SFMD-02**: Expression lambda body constructed via SyntaxFactory (not string interpolation)
- [ ] **SFMD-03**: Fluent chain (`.WhenTrue().WhenFalse().Create()`) constructed via nested `InvocationExpression`/`MemberAccessExpression`
- [ ] **SFMD-04**: All existing simple spec declaration tests pass unchanged

### Composed Spec Declaration Migration

- [x] **SFMC-01**: `CustomSpecDeclarationSyntax.CreateComposedInternal()` uses SyntaxFactory
- [x] **SFMC-02**: Block lambda with local variable declarations constructed via SyntaxFactory
- [x] **SFMC-03**: Nested record declaration constructed via SyntaxFactory
- [x] **SFMC-04**: Composition expression uses `ReplaceNodes` instead of `string.Replace` for clause name substitution
- [x] **SFMC-05**: All existing composed spec tests pass unchanged

### Constructor Spec Declaration Migration

- [ ] **SFMK-01**: `CustomSpecDeclarationSyntax.CreateWithConstructorInternal()` uses SyntaxFactory
- [ ] **SFMK-02**: Constructor parameter and instance method injection via SyntaxFactory
- [ ] **SFMK-03**: All existing constructor spec tests pass unchanged

### Orchestrator Cleanup

- [ ] **SFMO-01**: `ConvertToSpecCodeFix.ReplaceMultiVariableExpression` constructs field/method/constructor directly (no temp-class parse round-trip)
- [ ] **SFMO-02**: Comment trivia for original expressions added via SyntaxFactory trivia APIs
- [ ] **SFMO-03**: All existing multi-variable tests pass unchanged

### Dead Code Removal

- [ ] **SFMX-01**: Obsoleted string manipulation methods removed (`ToCamelCase` duplicate, `UpdateCompositionWithCamelCaseNames`, `ReplaceInstanceMethodCalls`)
- [ ] **SFMX-02**: `EscapeDoubleQuotes()` removed if `SyntaxFactory.Literal()` handles escaping
- [ ] **SFMX-03**: Full test suite passes after cleanup

## Out of Scope

| Feature | Reason |
|---------|--------|
| Changing generated output format | Pure refactoring — output must remain identical |
| `SyntaxGenerator` usage | C#-specific constructs (records, primary constructors) not supported |
| `Formatter.Format()` workspace dependency | Unnecessary — `NormalizeWhitespace()` is sufficient |
| Generic "Syntax Builder" abstraction | Over-engineering — direct SyntaxFactory is appropriate |
| Test output changes | Tests are the verification gate — they must pass as-is |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| SFMI-01 | Phase 7 | Complete |
| SFMI-02 | Phase 7 | Complete |
| SFMI-03 | Phase 7 | Complete |
| SFMD-01 | Phase 8 | Pending |
| SFMD-02 | Phase 8 | Pending |
| SFMD-03 | Phase 8 | Pending |
| SFMD-04 | Phase 8 | Pending |
| SFMC-01 | Phase 9 | Complete |
| SFMC-02 | Phase 9 | Complete |
| SFMC-03 | Phase 9 | Complete |
| SFMC-04 | Phase 9 | Complete |
| SFMC-05 | Phase 9 | Complete |
| SFMK-01 | Phase 10 | Pending |
| SFMK-02 | Phase 10 | Pending |
| SFMK-03 | Phase 10 | Pending |
| SFMO-01 | Phase 11 | Pending |
| SFMO-02 | Phase 11 | Pending |
| SFMO-03 | Phase 11 | Pending |
| SFMX-01 | Phase 12 | Pending |
| SFMX-02 | Phase 12 | Pending |
| SFMX-03 | Phase 12 | Pending |

**Coverage:**
- Refactor requirements: 21 total
- Mapped to phases: 21
- Unmapped: 0

**Coverage: 100%** — All requirements mapped to phases.

---
*Requirements defined: 2026-02-08*
*Last updated: 2026-02-09 -- Phase 7 requirements complete*
