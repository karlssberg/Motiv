---
phase: 07-specinvocation-migration
plan: 01
subsystem: codefix
tags: [roslyn, syntaxfactory, csharp, refactoring]

# Dependency graph
requires:
  - phase: 02-codefix-foundation
    provides: SpecInvocationSyntax.cs with ParseExpression-based implementation
provides:
  - SyntaxFactory-based SpecInvocationSyntax.Create() methods (3 overloads)
  - using static SyntaxFactory pattern in Syntax folder
  - CarriageReturnLineFeed constant access for subsequent phases
affects: [08-customspec-migration, 09-proposition-model-migration, 10-metadata-trivia, 11-inline-comments, 12-normalization-final]

# Tech tracking
tech-stack:
  added: []
  patterns: [leaf-to-root syntax construction, using static SyntaxFactory]

key-files:
  created: []
  modified: [src/Motiv.CodeFix/Syntax/SpecInvocationSyntax.cs]

key-decisions:
  - "SimpleNameSyntax as internal parameter type (covers IdentifierNameSyntax and GenericNameSyntax)"
  - "NormalizeWhitespace on final expression only (not intermediate nodes)"

patterns-established:
  - "SyntaxFactory construction: ObjectCreationExpression → MemberAccessExpression → InvocationExpression → MemberAccessExpression → NormalizeWhitespace"
  - "using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory for direct access to factory methods"

# Metrics
duration: 1.4min
completed: 2026-02-09
---

# Phase 7 Plan 01: SpecInvocation Migration Summary

**Replaced ParseExpression string interpolation with SyntaxFactory API construction for SpecInvocationSyntax (ObjectCreationExpression, MemberAccessExpression, InvocationExpression)**

## Performance

- **Duration:** 1.4 min
- **Started:** 2026-02-09T01:09:17Z
- **Completed:** 2026-02-09T01:10:43Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Eliminated ParseExpression and raw string interpolation from SpecInvocationSyntax.cs
- Established using static SyntaxFactory pattern in Syntax folder
- Confirmed CarriageReturnLineFeed available for subsequent phases
- All 46 passing CodeFix tests remain green with identical output

## Task Commits

Each task was committed atomically:

1. **Task 1: Replace ParseExpression with SyntaxFactory construction** - `95397b7` (refactor)

## Files Created/Modified
- `src/Motiv.CodeFix/Syntax/SpecInvocationSyntax.cs` - SyntaxFactory-based construction of `new SpecName().IsSatisfiedBy(model).Satisfied` expression

## Decisions Made

**1. Use SimpleNameSyntax for internal method parameter**
- **Rationale:** SimpleNameSyntax is the common base type of IdentifierNameSyntax and GenericNameSyntax, allowing single internal method to handle all three public Create overloads

**2. Apply NormalizeWhitespace on final expression only**
- **Rationale:** Roslyn best practice - normalize the complete syntax tree rather than intermediate nodes, allowing proper formatting with correct indentation and trivia

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - refactoring proceeded smoothly with green tests throughout.

## Next Phase Readiness

- Pattern established for SyntaxFactory construction (leaf-to-root building)
- using static SyntaxFactory available in Syntax folder
- CarriageReturnLineFeed constant accessible for trivia manipulation in phases 10-12
- Ready to proceed with CustomSpecDeclarationSyntax migration (phase 08)

---
*Phase: 07-specinvocation-migration*
*Completed: 2026-02-09*

## Self-Check: PASSED

All files and commits verified.
