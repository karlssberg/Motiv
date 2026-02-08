# Domain Pitfalls: SyntaxFactory Migration

**Domain:** Roslyn CodeFix migration from string-based code generation to SyntaxFactory
**Researched:** 2026-02-08
**Confidence:** HIGH (verified against codebase analysis + official Roslyn docs + community reports)

## Critical Pitfalls

Mistakes that cause test failures, incorrect output, or require significant rework.

### Pitfall 1: NormalizeWhitespace() Destroys Custom Trivia

**What goes wrong:** Calling `NormalizeWhitespace()` after attaching leading or trailing trivia (via `WithLeadingTrivia()`, `WithTrailingTrivia()`, or `WithTriviaFrom()`) silently overwrites all custom trivia with its own computed whitespace. The result compiles but has wrong formatting -- tests comparing exact output will fail.

**Why it happens:** `NormalizeWhitespace()` walks the entire syntax tree and replaces all trivia with computed defaults. It does not distinguish between "trivia I should fix" and "trivia the developer intentionally placed."

**Consequences:**
- Tests that verify exact generated output will fail with whitespace/indentation differences.
- Comments attached as leading trivia (e.g., `// originalExpression`) will be stripped or repositioned.
- Blank lines between members will be collapsed.

**This codebase is especially vulnerable because:** The tests in `MotivConvertToSpecTests.cs` verify **exact string output** including indentation, blank lines between clauses, and comment placement. Any trivia misstep breaks the test.

**Prevention:**
- Call `NormalizeWhitespace()` **first**, then attach custom trivia afterward.
- Or avoid `NormalizeWhitespace()` entirely and manage trivia manually per-node.
- When using `Formatter.Annotation` (preferred for code fixes), trivia is handled by the workspace formatter at application time, avoiding this conflict.

**Detection (warning signs):**
- Tests fail with "expected whitespace X, got whitespace Y" diffs.
- Generated code is left-justified (zero indentation) despite being nested.
- Comments disappear from generated output.

**Which migration step should address it:** Every step that produces output. Establish the trivia management strategy (manual vs. `Formatter.Annotation`) in the very first migration step and apply it consistently throughout.

---

### Pitfall 2: NormalizeWhitespace() Removes Intentional Blank Lines

**What goes wrong:** `NormalizeWhitespace()` removes all "unnecessary" `EndOfLineTrivia`, collapsing intentional blank lines between clause declarations, between members, and between class declarations.

**Why it happens:** Roslyn's normalizer treats consecutive blank lines as redundant trivia and strips them to a single newline. It has no concept of "intentional blank line for readability."

**Consequences:** The current string-based output includes blank lines between clauses in composed specs:

```csharp
var isAgePositive = Spec.Build((Model m) => m.Age > 0)
    .WhenTrue("age > 0 == true")
    .WhenFalse("age > 0 == false")
    .Create();
                                          // <-- blank line here
var isName = Spec.Build((Model m) => m.Name)
    .WhenTrue("name == true")
    ...
```

`NormalizeWhitespace()` will collapse these, breaking test assertions.

**Prevention:**
- After normalizing (or instead of normalizing), insert `EndOfLineTrivia` manually between clause variable declarations.
- Build the blank-line trivia as: `TriviaList(CarriageReturnLineFeed, CarriageReturnLineFeed)` as leading trivia on subsequent clause declarations.

**Detection:** Tests fail showing missing blank lines between clause `var` declarations.

**Which migration step should address it:** The `CustomSpecDeclarationSyntax` migration (composed/constructor spec creation), since that is where inter-clause blank lines appear.

---

### Pitfall 3: Line Ending Mismatch (CRLF vs LF)

**What goes wrong:** SyntaxFactory produces `\n` (LF) by default for `EndOfLineTrivia`, but the test infrastructure and Windows-based source code use `\r\n` (CRLF). Tests fail with invisible whitespace differences.

**Why it happens:** `SyntaxFactory.LineFeed` produces `\n`; `SyntaxFactory.CarriageReturnLineFeed` produces `\r\n`. It is easy to use the wrong one, or to mix them. The current code already reveals this problem -- see `ConvertToSpecCodeFix.cs` line 421:

```csharp
var tempUnit = ParseCompilationUnit(newMethodSource.Replace("\r\n", "\n").Replace("\n", "\r\n"));
```

This double-replace hack normalizes line endings before parsing. A pure SyntaxFactory approach must handle this consistently from the start.

**Consequences:** Tests pass on one OS but fail on another if line endings differ. Diffs show no visible difference but string comparison fails.

**Prevention:**
- Use `SyntaxFactory.CarriageReturnLineFeed` consistently (not `SyntaxFactory.LineFeed`) since the existing tests expect `\r\n`.
- Create a helper constant: `private static readonly SyntaxTrivia NewLine = SyntaxFactory.CarriageReturnLineFeed;`
- Never use `SyntaxFactory.EndOfLine("\n")` directly.

**Detection:** Tests fail with `Assert.Equal` showing identical-looking strings. Run `diff --color` or check `string.Contains("\r\n")` vs `string.Contains("\n")`.

**Which migration step should address it:** First migration step. Establish a `NewLine` constant/helper immediately.

---

### Pitfall 4: Missing Spaces Between Tokens (Keyword Concatenation)

**What goes wrong:** SyntaxFactory nodes are created without any whitespace between tokens by default. `public class Foo` becomes `publicclassFoo` unless whitespace trivia is explicitly added to each token.

**Why it happens:** SyntaxFactory creates minimal nodes. Tokens do not inherently include surrounding whitespace -- that is trivia. When building from scratch, every space must be explicitly provided as trailing or leading trivia.

**Consequences:** Generated code does not compile. Or worse, it compiles but is unreadable when tests compare string output.

**Prevention:**
- Use `Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(Space)` for each keyword.
- Or use `NormalizeWhitespace()` **on individual sub-expressions** (not the whole tree) to let Roslyn add minimum required spacing.
- Or use `Formatter.Annotation` which handles inter-token spacing automatically.
- The existing `PropositionModelSyntax.cs` reference implementation uses `NormalizeWhitespace()` at the class level (line 38) -- this works for that simple case but will conflict with custom trivia for more complex cases.

**Detection:** Generated code does not compile (diagnostic errors about unexpected tokens).

**Which migration step should address it:** Every step. This is the most basic SyntaxFactory requirement.

---

### Pitfall 5: Primary Constructor Syntax is Complex in SyntaxFactory

**What goes wrong:** The codefix generates classes with primary constructors (e.g., `public class Proposition() : Spec<int>(() => ...)`). Constructing this with pure SyntaxFactory is significantly more complex than the string representation suggests, because:

1. The primary constructor parameter list is part of `ClassDeclarationSyntax.ParameterList` (C# 12+).
2. The base class argument (the lambda `() => ...`) is part of `BaseListSyntax` > `PrimaryConstructorBaseTypeSyntax`.
3. The lambda body can be a block (for composed specs) or an expression (for simple specs).

**Why it happens:** Primary constructors have a different syntax tree structure than traditional constructors. The Roslyn API models them as parameters on the class declaration itself, with base arguments threaded through a specialized base type syntax node.

**Consequences:** Attempting to build this with SyntaxFactory without understanding the syntax tree structure leads to either:
- Wrong syntax tree (compiles but produces incorrect code)
- Runtime exceptions from SyntaxFactory (wrong node kind)
- Hours of debugging trying to figure out the correct nesting

**Prevention:**
- Use the [Roslyn Quoter](https://roslynquoter.azurewebsites.net/) to generate the SyntaxFactory calls for an example of the target output.
- Parse a representative example with `SyntaxFactory.ParseCompilationUnit()` and inspect the resulting tree with `.GetDiagnostics()` and the debugger to understand the structure.
- Consider a **hybrid approach**: use `ParseCompilationUnit` for the class shell (primary constructor + base list), then replace the body/lambda with SyntaxFactory-constructed nodes. This reduces risk for the most complex structural element.

**Detection:** The generated class doesn't inherit from `Spec<T>` or the constructor arguments are wrong.

**Which migration step should address it:** `CustomSpecDeclarationSyntax.CreateInternal` (simple spec) should be the first migration target since it is the simplest primary constructor case. Tackle `CreateComposedInternal` and `CreateWithConstructorInternal` after the pattern is established.

---

### Pitfall 6: Comment Preservation Requires Explicit Trivia Construction

**What goes wrong:** The codefix generates comments like `// valueA > valueB && valueC` above the replacement code. In string-based generation, this is trivial string interpolation. In SyntaxFactory, comments must be constructed as `SyntaxTrivia` and attached as leading trivia to the correct token or node.

**Why it happens:** `SyntaxFactory.Comment("// text")` creates a `SingleLineCommentTrivia`, but it must be:
1. Placed in the **leading trivia** of the first token of the statement following the comment.
2. Preceded by appropriate indentation whitespace trivia.
3. Followed by an `EndOfLineTrivia` before the actual statement's whitespace/indentation begins.

Getting the trivia ordering wrong produces comments on the wrong line, with wrong indentation, or concatenated with the following statement.

**Consequences:** Comments appear in wrong location, have wrong indentation, or are missing entirely.

**Prevention:**
- Build the complete leading trivia list in order: `[Whitespace(indent), Comment("// text"), EndOfLine, Whitespace(indent)]`
- The comment trivia attaches to the **next statement's leading trivia**, not as a standalone node.
- Test with assertions that check both the comment text and its position relative to surrounding code.

**Detection:** Comments appear on same line as code, or indentation is wrong in test output.

**Which migration step should address it:** The `ConvertToSpecCodeFix.ReplaceMultiVariableExpression` migration, since that is where `// originalExpression` comments are generated.

---

## Moderate Pitfalls

Mistakes that cause delays or require iteration but don't force a complete rework.

### Pitfall 7: Generic Type Names in BaseList Require Specific SyntaxFactory Construction

**What goes wrong:** The codefix generates `Spec<int>`, `Spec<Proposition.Model>`, `Spec<MyNamespace.Order>`, etc. In SyntaxFactory, `GenericNameSyntax` requires building a `TypeArgumentList` with the correct `TypeSyntax` nodes. Qualified names like `Proposition.Model` require `QualifiedNameSyntax`, not just `IdentifierNameSyntax`.

**Why it happens:** String interpolation naturally handles `Spec<Proposition.Model>` as a flat string. SyntaxFactory requires:

```csharp
GenericName("Spec")
    .WithTypeArgumentList(
        TypeArgumentList(
            SingletonSeparatedList<TypeSyntax>(
                QualifiedName(
                    IdentifierName("Proposition"),
                    IdentifierName("Model")))))
```

Developers often try `ParseTypeName("Proposition.Model")` as a shortcut, which works but mixes string-parsing with SyntaxFactory -- exactly what the migration is trying to eliminate.

**Prevention:**
- Build a helper method `TypeFromString(string typeName)` that parses dotted names into `QualifiedNameSyntax` chains.
- Or accept `ParseTypeName()` as a legitimate tool for type name construction even in a "pure SyntaxFactory" migration -- type names are inherently string-like.
- Document the decision: "String interpolation is acceptable for type names and literal values" (per PROJECT.md: "Maintain string interpolation only for runtime string literal values").

**Detection:** Compiler errors about missing type arguments or wrong type syntax.

**Which migration step should address it:** `CustomSpecDeclarationSyntax` migration, specifically the base class construction.

---

### Pitfall 8: Nested Record Type Inside Class Requires Careful Member Ordering

**What goes wrong:** The composed spec pattern nests a `record Model(...)` inside the class body, after the primary constructor's lambda body closes:

```csharp
public class Proposition() : Spec<Proposition.Model>(() =>
{
    // ... clauses ...
    return composition;
})
{
    public record Model(int X, int Y);  // nested inside class body
}
```

In SyntaxFactory, the class `Members` collection must include the record. But the base argument lambda is part of the `BaseList`, not the `Members`. Confusing where the lambda ends and the class body begins is a common mistake.

**Why it happens:** The syntax tree for this construct is:
- `ClassDeclaration` with `ParameterList` (empty `()`)
- `BaseList` containing `PrimaryConstructorBaseType` with `ArgumentList` containing the lambda
- `Members` containing the `RecordDeclaration`

The string representation makes it look like the lambda and the record are at the same level, but they are in completely different parts of the syntax tree.

**Prevention:**
- Build the class in stages: (1) create the record declaration, (2) create the lambda expression, (3) create the base type with the lambda as argument, (4) assemble the class with both base list and members.
- Use the Roslyn Quoter to verify the tree structure of a working example.

**Detection:** Record type appears outside the class, or the class has no members, or the base constructor argument is wrong.

**Which migration step should address it:** `CustomSpecDeclarationSyntax.CreateComposedInternal` and `CreateWithConstructorInternal`.

---

### Pitfall 9: Fluent Method Chain Indentation in Lambda Bodies

**What goes wrong:** The generated specs have fluent chains like:

```csharp
var clause1 = Spec.Build((Model m) => m.Age > 0)
    .WhenTrue("age > 0 == true")
    .WhenFalse("age > 0 == false")
    .Create();
```

Getting this indentation right with SyntaxFactory requires each method invocation in the chain to have correct leading trivia (whitespace for indentation). `NormalizeWhitespace()` will left-justify these, removing the indentation.

**Why it happens:** Fluent method chains are syntactically nested `InvocationExpression` > `MemberAccessExpression` > `InvocationExpression` structures. Each `.MethodName` is a `MemberAccessExpression` whose dot token needs leading newline + indentation trivia.

**Prevention:**
- For fluent chains, either:
  1. Build the chain with SyntaxFactory and apply `NormalizeWhitespace()` (which will put everything on one line), then manually insert line breaks at each `.` -- tedious and fragile.
  2. Build the chain as a string and `ParseExpression()` it -- simpler and the formatting comes from the string.
  3. Use `Formatter.Annotation` and let the workspace formatter handle indentation.
- Option 2 (parse the fluent chain from string) is pragmatic for this codebase since the chain content includes runtime string values (`WhenTrue`/`WhenFalse` descriptions) anyway.

**Detection:** Fluent chains appear on one line or have wrong indentation in test output.

**Which migration step should address it:** `CustomSpecDeclarationSyntax` -- clause generation within the lambda body.

---

### Pitfall 10: Using Directive Placement and Trivia

**What goes wrong:** When adding `using Motiv;` to the compilation unit, the new using directive must have correct trailing trivia (newline) and the subsequent code must maintain a blank line separator. Without this, the using appears concatenated with the namespace declaration.

**Why it happens:** `CompilationUnitSyntax.AddUsings()` adds the directive but does not automatically add line breaks between the new using and existing code.

**Prevention:**
- After adding the using, ensure trailing trivia includes `CarriageReturnLineFeed`.
- Consider using Roslyn's `AddImportsAnnotation` approach instead of manual using insertion -- attach `Simplifier.AddImportsAnnotation` to nodes that reference `Motiv` types, and Roslyn will add the using directive automatically with correct formatting.
- The current implementation (`AddUsingStatementsIfNeeded`) manually adds via `compilationUnit.AddUsings()`. If migrating, test that the blank line between `using Motiv;` and `namespace MyNamespace;` is preserved.

**Detection:** Using directive appears on the same line as the namespace, or there is no blank line between usings and namespace.

**Which migration step should address it:** `ConvertToSpecCodeFix.AddUsingStatementsIfNeeded` migration. Consider switching to annotation-based approach during this step.

---

### Pitfall 11: Formatter.Annotation Requires Workspace Access

**What goes wrong:** The recommended approach for code fix formatting (`Formatter.Annotation` + `Formatter.Format()`) requires a `Workspace` object. In a code fix provider, the workspace is available via `context.Document.Project.Solution.Workspace`. But `Formatter.Format()` is in `Microsoft.CodeAnalysis.Formatting` which is a Workspaces dependency -- already referenced by this project via `Microsoft.CodeAnalysis.CSharp.Workspaces`.

However, the formatting depends on the host IDE's formatting options. In test contexts (Roslyn's test infrastructure), the workspace is a mock/test workspace with default formatting. This means `Formatter.Annotation` produces output formatted with defaults, which may differ from the exact format the tests currently expect.

**Why it happens:** `Formatter.Annotation` delegates formatting to the workspace's formatting engine, which uses the workspace's options (indentation size, brace placement, etc.). Tests use a test workspace with potentially different defaults than the raw string approach.

**Consequences:** Tests may need adjustment if the Formatter produces slightly different indentation or spacing than the current string-based approach.

**Prevention:**
- Verify that the test workspace's formatting options match the expected output format.
- If using `Formatter.Annotation`, run one test first to compare output before migrating all.
- Alternatively, skip `Formatter.Annotation` and manage trivia manually -- more work but produces deterministic output matching existing tests.

**Detection:** Tests fail with indentation differences after switching to `Formatter.Annotation`.

**Which migration step should address it:** Decide the formatting strategy in the first migration step. If choosing `Formatter.Annotation`, update test expectations. If choosing manual trivia, document the patterns.

---

## Minor Pitfalls

Mistakes that cause annoyance but are fixable with small corrections.

### Pitfall 12: SyntaxFactory.Token() vs SyntaxFactory.Identifier()

**What goes wrong:** Confusing `Token(SyntaxKind.IdentifierToken)` with `Identifier("name")`. The former creates a generic identifier token without text; the latter creates a named identifier. Using the wrong one produces empty identifiers or runtime errors.

**Prevention:** Always use `SyntaxFactory.Identifier("name")` for named identifiers. Use `SyntaxFactory.Token(SyntaxKind.XKeyword)` only for keywords and punctuation.

**Which migration step should address it:** All steps -- this is basic SyntaxFactory literacy.

---

### Pitfall 13: SeparatedList vs SingletonSeparatedList vs List

**What goes wrong:** Roslyn uses different list types for different contexts. `ParameterList` expects a `SeparatedSyntaxList<ParameterSyntax>` (comma-separated), while `Members` expects a `SyntaxList<MemberDeclarationSyntax>`. Using the wrong list type causes compilation errors in the analyzer code itself.

**Prevention:**
- `SyntaxFactory.SeparatedList<T>()` for comma-separated items (parameters, arguments, type arguments).
- `SyntaxFactory.SingletonSeparatedList<T>()` for single-item separated lists.
- `SyntaxFactory.List<T>()` for non-separated lists (members, statements, accessors).
- Use the compiler -- if the code compiles, the list type is correct.

**Which migration step should address it:** All steps.

---

### Pitfall 14: ParseTypeName() for netstandard2.0 Type Names

**What goes wrong:** The codefix resolves type names via `ITypeSymbol.ToDisplayString()`, which produces fully-qualified names like `MyNamespace.Order`. When building SyntaxFactory nodes, these must be parsed into `QualifiedNameSyntax`. Using `IdentifierName("MyNamespace.Order")` produces a single identifier with a dot in it, which is syntactically wrong.

**Prevention:** Use `SyntaxFactory.ParseTypeName(typeString)` to convert display strings to proper `TypeSyntax`. This is one of the legitimate uses of string parsing even in a "pure SyntaxFactory" codebase -- type names from semantic analysis are inherently strings.

**Which migration step should address it:** `CustomSpecDeclarationSyntax` and `SpecInvocationSyntax` -- anywhere type names from semantic model are used.

---

### Pitfall 15: WithTriviaFrom() Only Copies Leading and Trailing

**What goes wrong:** `WithTriviaFrom(original)` copies only the leading trivia and trailing trivia from `original` to the new node. It does not copy trivia from descendant tokens within the node. If the original expression had internal trivia (e.g., spaces around operators), those are preserved in the descendant tokens of the *new* expression only if those descendants were also copied.

**Prevention:**
- For simple replacements (swapping one identifier for another), `WithTriviaFrom()` works correctly.
- For complex replacements (rebuilding an expression), ensure internal whitespace is handled at the token level, not just the outer node level.
- The existing code in `ConvertLogicVariablesToModelMemberAccess` correctly uses `WithTriviaFrom(original)` for member access replacements -- this pattern should be preserved during migration.

**Which migration step should address it:** `ConvertToSpecCodeFix` migration -- the variable replacement logic.

---

## Phase-Specific Warnings

| Phase/Step | Likely Pitfall | Mitigation |
|------------|---------------|------------|
| Establish trivia strategy | Pitfall 1 (NormalizeWhitespace destroys trivia), Pitfall 3 (CRLF vs LF) | Decide manual trivia vs Formatter.Annotation up front. Create NewLine constant. |
| `CustomSpecDeclarationSyntax.CreateInternal` (simple spec) | Pitfall 5 (primary constructor complexity), Pitfall 7 (generic types in base list) | Use Roslyn Quoter to understand tree structure. Consider hybrid parse+construct. |
| `CustomSpecDeclarationSyntax.CreateComposedInternal` (composed spec) | Pitfall 2 (blank lines removed), Pitfall 8 (nested record), Pitfall 9 (fluent chain indentation) | Build in stages: record, lambda, base type, class. Insert blank line trivia manually. |
| `CustomSpecDeclarationSyntax.CreateWithConstructorInternal` (constructor spec) | Pitfall 5 (primary constructor with parameters), Pitfall 8 (nested record) | Same staged approach as composed, plus constructor parameter handling. |
| `SpecInvocationSyntax` migration | Pitfall 4 (missing spaces), Pitfall 14 (type name parsing) | Keep ParseTypeName for type strings. Verify inter-token spacing. |
| `ConvertToSpecCodeFix` method body replacement | Pitfall 6 (comment preservation), Pitfall 15 (WithTriviaFrom scope) | Build comment trivia list explicitly. Preserve existing WithTriviaFrom patterns. |
| Using directive management | Pitfall 10 (using placement trivia) | Consider annotation-based approach. Test blank line between usings and namespace. |
| All steps | Pitfall 3 (CRLF), Pitfall 4 (token spacing), Pitfall 12 (Token vs Identifier), Pitfall 13 (list types) | Consistent conventions. Run tests after every small change. |

## Strategic Recommendation: Hybrid Approach

Based on this analysis, a **pure SyntaxFactory approach for every construct is high-risk** for this codebase because:

1. The tests verify exact string output, making trivia precision critical.
2. The constructs (primary constructors with lambda base arguments, fluent chains, nested records) are among the most complex SyntaxFactory patterns.
3. The existing string-based approach already produces correct output.

**Recommended migration strategy:**

1. **Use SyntaxFactory for structural elements**: class declarations, member declarations, parameter lists, type references -- where SyntaxFactory adds value (type safety, integration with target codebase formatting).

2. **Keep ParseExpression/ParseTypeName for embedded expressions**: Lambda bodies, fluent chain arguments, type names from semantic analysis, and string literal values -- where strings are the natural representation.

3. **Manual trivia for formatting**: Use explicit trivia construction (not `NormalizeWhitespace()`) for inter-member spacing, comment placement, and indentation -- gives deterministic output matching existing tests.

4. **Validate incrementally**: Migrate one method at a time, run the full test suite after each. Never migrate two methods simultaneously.

## Sources

### Official Documentation
- [Microsoft: Get started with syntax transformation](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/syntax-transformation)
- [Microsoft: Adding a Code Fix to Your Roslyn Analyzer](https://learn.microsoft.com/en-us/archive/msdn-magazine/2015/february/csharp-adding-a-code-fix-to-your-roslyn-analyzer)
- [SyntaxFactory.Comment API docs](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.comment?view=roslyn-dotnet-4.13.0)
- [SyntaxFactory.ElasticMarker API docs](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.elasticmarker?view=roslyn-dotnet-4.13.0)

### Community Resources (verified against official docs)
- [Getting whitespace right with Roslyn CSharpSyntaxRewriter](https://criticalhittech.com/2019/03/19/getting-whitespace-right-with-roslyn-csharpsyntaxrewriter/) - NormalizeWhitespace timing, block braces management
- [Generating C# code with Roslyn APIs (Jeremy Davis, 2024)](https://blog.jermdavis.dev/posts/2024/csharp-code-with-roslyn) - Practical SyntaxFactory gotchas
- [Roslyn Tips: Automatically adding using directives](https://bytesbyacker.com/articles/roslyn-adding-using-directives) - AddImportsAnnotation approach
- [Roslyn Quoter](https://roslynquoter.azurewebsites.net/) - Tool for generating SyntaxFactory calls from C# code

### Roslyn GitHub Issues (verified behavior reports)
- [NormalizeWhitespace preserving line feeds (#24827)](https://github.com/dotnet/roslyn/issues/24827) - Blank line removal behavior
- [NormalizeWhitespace performance (#54144)](https://github.com/dotnet/roslyn/issues/54144) - Performance concerns
- [NormalizeWhitespace breaks XML attributes (#47363)](https://github.com/dotnet/roslyn/issues/47363) - Formatting bugs
- [Formatter annotation indentation (#2557)](https://github.com/dotnet/roslyn/issues/2557) - Formatter.Annotation behavior
- [Source generators feedback on SyntaxFactory (#43979)](https://github.com/dotnet/roslyn/issues/43979) - SyntaxFactory complexity
- [BaseList API confusion (#19316)](https://github.com/dotnet/roslyn/issues/19316) - Base type syntax issues

### Codebase Analysis
- `PropositionModelSyntax.cs` -- existing SyntaxFactory reference implementation using `NormalizeWhitespace()` (line 38)
- `CustomSpecDeclarationSyntax.cs` -- primary target, uses StringBuilder + ParseCompilationUnit (lines 147-206, 311-348, 412-433)
- `SpecInvocationSyntax.cs` -- uses ParseExpression + NormalizeWhitespace (line 30)
- `ConvertToSpecCodeFix.cs` -- uses ParseCompilationUnit with CRLF normalization (line 421), raw string method bodies (lines 391-419, 458-466)
- `MotivConvertToSpecTests.cs` -- 11 test cases verifying exact string output
