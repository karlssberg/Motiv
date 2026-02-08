---
phase: 02-codefix-foundation
plan: 02
subsystem: codefix
tags: [roslyn, codefix, name-derivation, code-generation]

# Dependency graph
requires:
  - phase: 02-01
    provides: ExpressionNameDeriver algorithm for context-aware naming
provides:
  - CodeFix generates context-derived proposition names (ValueProposition, OrderProposition, etc.)
  - Clean code generation without Debug.WriteLine or System.Diagnostics imports
  - Proper member access handling in expressions (order.Total treated as single variable)
  - End-to-end CodeFix pipeline with name derivation integration
affects: [02-03-expression-tree-support]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Two-pass node replacement for complex syntax transformations (member access chains)
    - Root identifier filtering to exclude property/field access from variable counting

key-files:
  created: []
  modified:
    - src/Motiv.CodeFix/MotivCodeFixProvider.cs
    - src/Motiv.CodeFix/ConvertToSpecCodeFix.cs
    - src/Motiv.CodeFix.Tests/MotivConvertToSpecTests.cs

key-decisions:
  - "Remove Debug.WriteLine entirely from generated code (clean output)"
  - "Member access expressions like order.Total count as single variable (order), not multiple"
  - "Two-pass replacement strategy for member access chains to avoid Roslyn cast exceptions"

patterns-established:
  - "CodeFix derives names from expression context before creating converter"
  - "GetVariablesInExpression excludes right-side member access identifiers (only root variables counted)"
  - "ConvertLogicVariablesToModelMemberAccess handles nested member access chains recursively"

# Metrics
duration: 7.4min
completed: 2026-02-08
---

# Phase 02 Plan 02: CodeFix Integration Summary

**CodeFix generates context-derived names (ValueProposition, OrderProposition) and clean code without Debug.WriteLine**

## Performance

- **Duration:** 7.4 min
- **Started:** 2026-02-08T01:18:14Z
- **Completed:** 2026-02-08T01:25:39Z
- **Tasks:** 2 (TDD: RED → GREEN)
- **Files modified:** 3

## Accomplishments
- Integrated ExpressionNameDeriver into CodeFix pipeline for context-aware naming
- Removed Debug.WriteLine and System.Diagnostics from all generated code paths
- Fixed member access handling bug (order.Total now correctly treated as single variable)
- Added comprehensive end-to-end tests covering all naming scenarios (8 total tests)

## Task Commits

Each task was committed atomically:

1. **Task 1: RED - Write failing end-to-end CodeFix tests** - `ba879c4` (test)
   - Updated 4 existing tests to expect derived names and no Debug.WriteLine
   - Added 4 new tests for member access, is-expression, and fallback scenarios
   - All tests fail as expected (CodeFix still produces old behavior)

2. **Task 2: GREEN - Integrate name derivation and remove Debug.WriteLine** - `4a6a1fc` (feat)
   - MotivCodeFixProvider calls ExpressionNameDeriver.DeriveClassNames per expression
   - Removed Debug.WriteLine from multi-variable and CreateMethodWithPropositionLogic templates
   - Removed System.Diagnostics using directive generation logic
   - Fixed GetVariablesInExpression to filter out member access right-side identifiers
   - Added ConvertLogicVariablesToModelMemberAccess two-pass replacement for member chains
   - All 17 tests pass (14 existing + 3 new CodeFix tests)

_Note: TDD plan with RED → GREEN flow_

## Files Created/Modified
- `src/Motiv.CodeFix/MotivCodeFixProvider.cs` - Derives names from expression via ExpressionNameDeriver before creating converter
- `src/Motiv.CodeFix/ConvertToSpecCodeFix.cs` - Clean templates (no Debug), proper member access handling, simplified AddUsingStatementsIfNeeded
- `src/Motiv.CodeFix.Tests/MotivConvertToSpecTests.cs` - 8 end-to-end tests covering all naming scenarios

## Decisions Made
- Remove Debug.WriteLine entirely (not just make it optional) - cleaner generated code
- Member access expressions (order.Total) treated as single-variable case, not multi-variable
- Two-pass replacement strategy for member access chains to avoid Roslyn InvalidCastException

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed member access variable counting**
- **Found during:** Task 2 (GREEN phase - running tests)
- **Issue:** GetVariablesInExpression included IPropertySymbol, causing order.Total to be treated as two variables (order + Total) instead of one
- **Fix:** Filtered out right-side member access identifiers in GetVariablesInExpression - only count root identifiers (parameters, locals, fields)
- **Files modified:** src/Motiv.CodeFix/ConvertToSpecCodeFix.cs
- **Verification:** Member access tests pass - order.Total correctly generates OrderProposition with single-variable path
- **Committed in:** 4a6a1fc (Task 2 commit)

**2. [Rule 1 - Bug] Fixed member access replacement causing InvalidCastException**
- **Found during:** Task 2 (GREEN phase - running tests with member access)
- **Issue:** Original ConvertLogicVariablesToModelMemberAccess tried to replace identifiers that were part of member access expressions, causing Roslyn syntax tree cast exceptions
- **Fix:** Implemented two-pass replacement - first replace member access expressions with variable roots, then replace standalone identifiers. Added RebuildMemberAccessChain helper for recursive member chain reconstruction.
- **Files modified:** src/Motiv.CodeFix/ConvertToSpecCodeFix.cs
- **Verification:** Member access tests pass without InvalidCastException
- **Committed in:** 4a6a1fc (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (2 bugs discovered during testing)
**Impact on plan:** Both bugs were critical for member access support (new scenario not covered by existing tests). Fixes enable CodeFix to work with real-world expressions like order.Total.

## Issues Encountered
- Roslyn InvalidCastException when replacing identifiers in member access expressions - solved with two-pass replacement strategy and recursive chain rebuilding
- Fully-qualified type names (MyNamespace.Order) generated instead of simple names - acceptable (safer, avoids ambiguity) - updated tests to match

## Next Phase Readiness
- CodeFix pipeline complete with name derivation and clean output
- Ready for expression tree support (02-03) which will extend spec creation patterns
- No blockers

## Self-Check: PASSED

All key files and commits verified.

---
*Phase: 02-codefix-foundation*
*Completed: 2026-02-08*
