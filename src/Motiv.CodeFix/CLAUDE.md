# Motiv.CodeFix / Motiv.Analyzer

Conventions for the Roslyn projects. Loaded when working in this subtree; the repo-root `CLAUDE.md`
carries the project-wide rules.

## Conventions

- Use `ParseTypeName(typeName)` instead of `IdentifierName(typeName)` when constructing type syntax nodes — `IdentifierName("int")` creates an `IdentifierNameSyntax`, but the test framework expects `PredefinedTypeSyntax` for C# keyword types like `int`, `string`, `bool`
- When creating semicolon-terminated class declarations (no body), explicitly suppress braces with `.WithOpenBraceToken(Token(SyntaxKind.None)).WithCloseBraceToken(Token(SyntaxKind.None))`
- Do not rely on `node.Ancestors()` inside `CSharpSyntaxRewriter` overrides — ancestor context is unreliable during tree rewriting. Instead, create separate rewriter classes for structurally different cases (e.g., block-lambda vs expression-lambda)
- When targeting specific lambda expressions in a `CSharpSyntaxRewriter`, use structural properties (e.g., parameter count, body type) rather than parent-type checks, since multiple lambdas in a chain can share the same parent type
- In C# primary constructor inheritance, access forwarded parameters via the base class's properties (not the subclass constructor parameter) to avoid CS9107 dual-capture warnings
- `NormalizeWhitespace()` strips all custom trivia — do not add formatting trivia during syntax construction if a `NormalizeWhitespace` + rewriter pass will follow. Apply formatting exclusively in the rewriter to avoid duplicate or dead formatting logic.
- New test files for CodeFix tests must use LF line endings (not CRLF) — the existing test files and `SpecInvocationReplacer.cs` use LF, and raw string literal content normalizes to `\n` at runtime. CRLF test files cause `<CR><LF>` vs `<LF>` mismatches in the Roslyn test framework's string comparison.
- When writing CodeFix tests with expected output, run the test once first to capture the actual generated output, then write the expectation to match — don't guess indentation levels for multi-line generated code.
- The "Convert to Motiv specification (with debug output)" CodeAction uses `.Tap()` with `Debug.WriteLine` — NOT a custom `DebugTap` method. `Debug.WriteLine` is `[Conditional("DEBUG")]` so the callback body is stripped in Release builds, making a dedicated debug extension unnecessary.
- `Motiv.CodeFix` and `Motiv.Analyzer` target `netstandard2.0` — do not use C# 8+ features that require runtime support (ranges `[..^n]`, `System.Index`, `System.Range`, default interface methods). Use `Substring()` instead of range indexing.
- In `ExpressionDecomposer`, clause names must be derived from the **original** expression, not the transformed one — transformations may add qualifiers (e.g., `Playground.IsGreen`) that produce mangled names and break `ClauseSet.ResolveComposition` substring replacement
- Known CodeFix edge cases to watch for: (1) `nameof()` is syntactically an `InvocationExpressionSyntax` — `InstanceMethodDetector` excludes it via `SyntaxFacts.GetContextualKeywordKind`; other keywords (`typeof`, `sizeof`, `default`) have their own syntax node types and don't reach `VisitInvocationExpression`; (2) pattern-introduced variables (`obj is string s`) must not be treated as method parameters in model generation; (3) when the diagnosed expression is nested inside `!()`, the negation context must be preserved in the replacement
- Use `SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None` to detect contextual keywords (e.g., `nameof`) rather than hard-coding string comparisons
- For CodeFix edge case tests with uncertain output, write the test source input first, run it with a placeholder expected output, then capture the actual output from the diff — don't spend time predicting exact indentation or comment wrapping
