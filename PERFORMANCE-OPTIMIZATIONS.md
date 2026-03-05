# Motiv Performance Optimizations Tracker

## Quick Wins

### 1. Double-enumeration in HigherOrder Evaluation classes
- **Status:** DONE
- **Files:** `HigherOrderBooleanResultEvaluation.cs`, `HigherOrderPolicyResultEvaluation.cs`, `HigherOrderBooleanEvaluation.cs`
- **Issue:** `_lazyTrueModels` / `_lazyFalseModels` re-filter the source with `WhereTrue()` / `WhereFalse()` instead of projecting from the already-computed `_lazyTrueResults.Value` / `_lazyFalseResults.Value`.
- **Fix:** Project models from the cached results array instead of re-filtering.

### 2. GroupBy for boolean partition in Causes.Resolve
- **Status:** DONE
- **File:** `HigherOrderProposition/Causes.cs`
- **Issue:** `GroupBy(...).Select(g => g.ToArray()).ToArray()` allocates GroupByIterator + groupings + arrays for a simple true/false split.
- **Fix:** Replace with a two-list manual partition loop.

### 3. Causes.Count() > 1 in BinaryBooleanResultDescription
- **Status:** DONE
- **File:** `Shared/BinaryBooleanResultDescription.cs`
- **Issue:** `result.Causes.Count() > 1` enumerates the full Causes sequence. `HasAtLeast(2)` already exists.
- **Fix:** Replace `Count() > 1` with `HasAtLeast(2)`.

### 4. Indent() uses Enumerable.Repeat + string.Join
- **Status:** DONE
- **File:** `Shared/IndentStringExtensions.cs`
- **Issue:** For `levelOfIndentation = 1` (the common case), allocates RepeatIterator + IEnumerable + string.Join. For deeper levels, same overhead multiplied.
- **Fix:** Pre-compute indent strings for common levels (0-8), index into the array.

### 5. ContainsReservedCharacters uses .Any(lambda)
- **Status:** DONE
- **File:** `StringExtensions.cs`
- **Issue:** `text.Any(c => Characters.Contains(c))` allocates a CharEnumerator via IEnumerable<char>.
- **Fix:** Use `foreach` over `string` which uses the stack-allocated `String.Enumerator` struct.

## Medium Effort

### 6. Lazy<T> thread-safety mode
- **Status:** DONE
- **Files:** All proposition types (37 files)
- **Issue:** Every proposition creates 3-7 `Lazy<T>` per evaluation using default `ExecutionAndPublication` mode (double-checked locking). Evaluation is single-threaded.
- **Fix:** Switch to `LazyThreadSafetyMode.None` to eliminate synchronization overhead.

### 7. Uncached Explanation.Underlying / AllUnderlying
- **Status:** DONE
- **File:** `Shared/Explanation.cs`
- **Issue:** `.Underlying` and `.AllUnderlying` re-run full tree walks with HashSet dedup + SequenceEqual on every access.
- **Fix:** Cache behind `_underlying ??=` and `_allUnderlying ??=` backing fields.

### 8. Uncached ResultDescriptionBase.Justification
- **Status:** DONE
- **File:** `ResultDescriptionBase.cs`
- **Issue:** `string.Join(Environment.NewLine, GetJustificationAsLines())` re-walks entire result tree on every access.
- **Fix:** Add `_justification ??=` backing field.

### 9. Uncached AllAssertions on BooleanResultBase
- **Status:** DONE
- **File:** `BooleanResultBase.cs`
- **Issue:** `Assertions` is cached with `_assertions ??=`, but `AllAssertions`, `SubAssertions`, `UnderlyingExpressionResults` etc. are recomputed every time.
- **Fix:** Add `_allAssertions ??=`, `_subAssertions ??=`, `_underlyingExpressionResults ??=` backing fields with `.ToArray()` materialization.

### 10. SequenceEqual on non-materialized LINQ chain in Explanation.ResolveUnderlying
- **Status:** DONE
- **Files:** `Shared/Explanation.cs`, `Shared/MetadataNode.cs`
- **Issue:** `underlyingAssertions` is a deferred LINQ chain passed to `SequenceEqual`, causing double enumeration.
- **Fix:** Materialize to array before comparing.

### 11. Uncached Values in HigherOrderPolicyResultEvaluation
- **Status:** DONE
- **File:** `HigherOrderProposition/HigherOrderPolicyResultEvaluation.cs`
- **Issue:** `Values` property creates a new SelectIterator on every access.
- **Fix:** Cache with `_lazyValues` Lazy field.

## Quick Wins (continued)

### 14. Uncached `Reason` on result description types
- **Status:** DONE
- **Files:** `Shared/BinaryBooleanResultDescription.cs`, `XOr/XOrBooleanResultDescription.cs`, `Not/NotBooleanResultDescription.cs`
- **Issue:** `Reason` is recomputed on every property access. `BinaryBooleanResultDescription.Reason` runs `string.Join(Separator, _causalResults.Select(ExplainReasons))` — allocating a SelectIterator, calling `HasAtLeast(2)` per element, and building a new string. `XOrBooleanResultDescription.Reason` does `string.Join(" ^ ", ...)` with recursive `ContainsBinaryOperation()` tree walks. `NotBooleanResultDescription.Reason` calls `FormatReason()` which traverses the NOT-chain via a while loop. All three feed `ToString()` and `[DebuggerDisplay("{Reason}")]`.
- **Fix:** Add `private string? _reason` backing field with `_reason ??=` pattern on each type.

### 15. Uncached `ExpressionDescription.Statement` and `ToReason`
- **Status:** DONE
- **File:** `Shared/ExpressionDescription.cs`
- **Issue:** `Statement` calls `statement.Serialize()` on every access — instantiating a new `CSharpExpressionSerializer`, allocating a `StringBuilder`, and walking the entire expression tree via visitor dispatch. `ToReason(bool)` calls `statement.ToExpressionAssertion(satisfied).Serialize()` — same cost plus expression mutation. Neither is cached. Compare with `ExpressionTreeDescription` which correctly caches `Statement` in its init: `public string Statement { get; } = expression.Body.Serialize();`.
- **Fix:** Cache `Statement` as a constructor-initialized property. Cache `ToReason` results for both true/false in lazy fields.

### 16. Uncached `Detailed` on spec description types
- **Status:** PENDING
- **Files:** `Shared/SpecDescription.cs`, `Shared/BinarySpecDescription.cs`, `Not/NotSpecDescription.cs`, `ExpressionTreeProposition/ExpressionAsStatementDescription.cs`, `ExpressionTreeProposition/ExpressionTreeDescription.cs`, `Shared/ExpressionDescription.cs`
- **Issue:** `Detailed` calls `string.Join(Environment.NewLine, GetDetailsAsLines())` on every access. `GetDetailsAsLines()` is a generator that recursively walks the spec tree, calling `Indent()` per line. Not cached — every access re-walks and re-allocates. Note: `ResultDescriptionBase.Justification` correctly uses `_justification ??=`, but `Detailed` on spec descriptions does not follow this pattern.
- **Fix:** Add `private string? _detailed` backing field with `_detailed ??=` on each type.

### 17. Uncached `Statement` on `BinarySpecDescription` and `NotSpecDescription`
- **Status:** PENDING
- **Files:** `Shared/BinarySpecDescription.cs`, `Not/NotSpecDescription.cs`
- **Issue:** `BinarySpecDescription.Statement` returns `$"{Summarize(left)} {operatorSymbol} {Summarize(right)}"` — allocating an interpolated string and calling `Summarize()` (which pattern-matches and may produce `$"({binarySpec.Description.Statement})"`) on every access. `NotSpecDescription.Statement` calls `FormatStatement()` which runs `operand.Name.FirstOrDefault()`, pattern-matches, and may call `ContainsBinaryOperation()` — a recursive spec tree traversal. Both feed `Detailed`, `ToReason()`, `GetDetailsAsLines()`, and `ToString()`.
- **Fix:** Cache `Statement` with `_statement ??=` backing field.

### 18. `Assertions` on `BooleanResultBase` caches `IEnumerable<string>` not `string[]`
- **Status:** PENDING
- **File:** `BooleanResultBase.cs`
- **Issue:** `Assertions` is `_assertions ??= Explanation.Assertions.DistinctWithOrderPreserved()`. `DistinctWithOrderPreserved()` returns a yielded `IEnumerable<string>` backed by a `HashSet`. Because the cache stores the lazy enumerable (not a materialized array), every subsequent iteration of `Assertions` re-runs the HashSet dedup pass. Compare with `SubAssertions` and `AllSubAssertions` which correctly add `.ToArray()`.
- **Fix:** Add `.ToArray()` to materialize: `_assertions ??= Explanation.Assertions.DistinctWithOrderPreserved().ToArray()`.

### 19. Uncached `UnderlyingReasons` on `BooleanResultBase`
- **Status:** PENDING
- **File:** `BooleanResultBase.cs`
- **Issue:** `UnderlyingReasons => UnderlyingExpressionResults.Select(result => result.Reason)` — creates a new SelectIterator on every access, even though `UnderlyingExpressionResults` is already cached as an array.
- **Fix:** Cache with `_underlyingReasons ??= UnderlyingExpressionResults.Select(result => result.Reason).ToArray()`.

### 20. Uncompiled `Regex.Split` in `VisitStringInterpolation`
- **Status:** PENDING
- **File:** `ExpressionTreeProposition/CSharpExpressionSerializer.cs` (line ~462)
- **Issue:** `Regex.Split(format, @"(?<=[{])(\s*?\d+\s*?)(?=[:}])")` uses a non-compiled regex pattern. Each call either allocates a new `Regex` internally or hits the static cache with lock contention.
- **Fix:** Extract to a `private static readonly Regex` with `RegexOptions.Compiled`.

## Medium Effort (continued)

### 21. Uncached `UnderlyingAssertionSources` and `UnderlyingAllAssertionSources`
- **Status:** PENDING
- **File:** `BooleanResultBase.cs`
- **Issue:** Both properties return unevaluated LINQ chains (`Causes.SelectMany(...).ElseIfEmpty(...)`) on every access. They are accessed recursively during `Explanation.ResolveUnderlying` and `MetadataNode.ResolveUnderlying` — so in an AND/OR tree of depth N, the chain is rebuilt O(N) times during the initial lazy evaluation.
- **Fix:** Add `_underlyingAssertionSources ??=` and `_underlyingAllAssertionSources ??=` backing fields with `.ToArray()`.

### 22. Uncached `RootAssertions` and `AllRootAssertions`
- **Status:** PENDING
- **File:** `BooleanResultBase.cs`
- **Issue:** `RootAssertions` calls `this.GetRootAssertions()` and `AllRootAssertions` calls `this.GetAllRootAssertions()` — both extension methods that recursively walk the entire result tree via `MetadataTier.Underlying`. No caching; every access re-traverses.
- **Fix:** Add `_rootAssertions ??=` and `_allRootAssertions ??=` backing fields.

### 23. `Causes`/`CausesWithValues` re-run `GetCausalResults()` generator on every access
- **Status:** PENDING
- **File:** `Shared/BinaryBooleanResult.cs`
- **Issue:** `Causes => GetCausalResults()` and `CausesWithValues => GetCausalResults()` are expression-bodied properties that return a new generator iterator on every call. `GetCausalResults()` is invoked multiple times per result lifecycle: once for `Description` construction, once for `Explanation` construction, and twice for `MetadataTier` construction (`CausesWithValues.GetValues()` + `CausesWithValues` as constructor arg). That's 4-5 generator re-executions for a 0-2 element sequence.
- **Fix:** Cache the results in a backing field: `_causalResults ??= GetCausalResults().ToArray()`. Then have both `Causes` and `CausesWithValues` return the cached array.

### 24. `Underlying`/`UnderlyingWithValues` re-run `GetAllResults()` generator
- **Status:** PENDING
- **File:** `Shared/BinaryBooleanResult.cs`
- **Issue:** Same pattern — `Underlying => GetAllResults()` allocates a new generator on every access. Used by `Explanation`, `AllAssertions`, `UnderlyingAllAssertionSources`.
- **Fix:** Cache: `_allResults ??= GetAllResults().ToArray()`.

### 25. Double-enumeration of `CausesWithValues` in `MetadataTier` construction
- **Status:** PENDING
- **File:** `Shared/BinaryBooleanResult.cs`
- **Issue:** `_metadataTier ??= new(CausesWithValues.GetValues(), CausesWithValues)` — calls `CausesWithValues` twice (two separate generator executions). Inside `MetadataNode`'s constructor, the `causes` parameter is enumerated again (`causes as T[] ?? causes.ToArray()` on line 72), and then iterated again within `ResolveUnderlying`. Total: the same 0-2 element generator runs 3-4 times.
- **Fix:** Depends on #23 — once `CausesWithValues` returns a cached array, the double-enumeration disappears.

### 26. `BooleanResultsCollection._satisfied` is an unevaluated `Where` — re-scanned per `GetEnumerator()`
- **Status:** PENDING
- **File:** `BooleanResultsCollection.cs`
- **Issue:** `_satisfied = results.Where(r => r.Satisfied)` stores a lazy LINQ chain. Every call to `GetEnumerator()` (i.e., every `foreach`) re-runs the `Where` filter over the original `results` sequence. Similarly, `Models`, `Values`, and `Assertions` cache `IEnumerable<T>` chains (not materialized arrays), so every iteration of those properties re-scans the source.
- **Fix:** Materialize `_satisfied` to an array in the constructor. Consider materializing `_models`, `_values`, `_assertions` with `.ToArray()` as well.

### 27. Double `.ToArray()` via `Causes.Resolve` then callers
- **Status:** PENDING
- **Files:** `HigherOrderProposition/Causes.cs`, multiple HigherOrder proposition files
- **Issue:** `Causes.Resolve` internally partitions into `trueList.ToArray()` / `falseList.ToArray()`. Then the calling propositions (e.g., `HigherOrderFromBooleanPredicateProposition`) do `causeSelector(isSatisfied, underlyingResults).ToArray()` — materializing the already-materialized array a second time.
- **Fix:** Either have `Causes.Resolve` return `List<T>` (avoiding the first `.ToArray()`), or remove the `.ToArray()` at the call sites.

### 28. `Causes.Resolve` uses `List<T>` + `.ToArray()` double allocation
- **Status:** PENDING
- **File:** `HigherOrderProposition/Causes.cs`
- **Issue:** Partitions into `List<T>` then immediately calls `.ToArray()`. The `List<T>` is discarded after the copy.
- **Fix:** Pre-size arrays using `operandResultArray.Length` as upper bound, or keep as `List<T>` throughout (avoiding the array copy) if downstream only needs `IEnumerable<T>`.

### 29. Thin `Lazy<T>` wrappers in higher-order proposition types
- **Status:** DONE (subsumed by #12)
- **File:** `HigherOrderProposition/BooleanResultPredicate/HigherOrderFromBooleanResultProposition.cs` (and similar)
- **Issue:** Several `Lazy<T>` fields are trivial wrappers: `metadataAsEnumerable = new Lazy<IEnumerable<TMetadata>>(() => metadata.Value.ToEnumerable())`, `assertionAsEnumerable = new Lazy<IEnumerable<string>>(() => assertion.Value.ToEnumerable())`, `causesAsUnderlying = new Lazy<IEnumerable<BooleanResultBase<TUnderlyingMetadata>>>(() => causes.Value)`. Each allocates a `Lazy<T>` + closure + delegate for wrapping a single value.
- **Fix:** Eliminated as part of #12 — pass-through Lazys were inlined as lambdas when result type constructors changed from `Lazy<T>` to `Func<T>`.

### 30. `SortedSet<TMetadata>` used when `HashSet<TMetadata>` would suffice
- **Status:** PENDING
- **File:** `Shared/MetadataNode.cs` (line ~31)
- **Issue:** `_lazyMetadataSet` branches on `IEnumerable<IComparable<TMetadata>>` and creates a `SortedSet<TMetadata>`. Since `string` implements `IComparable<string>`, the most common metadata type always gets a `SortedSet` — which has O(log n) operations vs `HashSet`'s O(1). For small assertion sets (1-3 items), both are fast, but `SortedSet` has higher constant overhead.
- **Fix:** Use `HashSet<TMetadata>` for all cases unless ordering is actually required by consumers.

### 31. `DynamicInvoke()` in `ExpressionTreeTransformer.TransformSpecExpression`
- **Status:** PENDING
- **File:** `ExpressionTreeProposition/ExpressionTreeTransformer.cs` (line ~325)
- **Issue:** `Expression.Lambda(specExpression).Compile().DynamicInvoke()` uses reflection-based invocation, which boxes value types and is slower than strongly-typed delegate calls. Runs once per spec construction (not per evaluation), so impact is build-time only.
- **Fix:** Use `Expression.Lambda<Func<object>>(Expression.Convert(specExpression, typeof(object))).Compile()()` for strongly-typed invocation.

## Higher Effort

### 12. Replace Lazy<T> with null-check caching in result types
- **Status:** DONE
- **Files:** 51 files across result types, proposition callers, evaluation contexts, and MetadataNode
- **Issue:** Every evaluation allocates 3-7 Lazy<T> objects (48-80 bytes each) plus closure delegates.
- **Fix:** Replaced `Lazy<T>` constructor parameters with `Func<T>` in result types, using `_field ??= factory()` for on-demand caching. Eliminated internal `Lazy<T>` fields in evaluation context types and MetadataNode. Inlined lambdas at call sites where `new Lazy<T>(...)` wrappers were single-use. Net reduction of ~263 lines. Also subsumed #29 (thin Lazy wrappers) and #33 (evaluation closure allocations).

### 13. ToEnumerable() single-element array allocations
- **Status:** DONE
- **Files:** Multiple higher-order proposition types
- **Issue:** Single values wrapped in `new[] { item }` inside Lazy delegates.
- **Fix:** Struct-based SingletonEnumerable<T> or store T[] directly.

### 32. `Expression.Lambda(...).Compile()` per evaluation in `CSharpExpressionSerializer<T>.VisitSerializeAsValue`
- **Status:** DONE
- **File:** `ExpressionTreeProposition/CSharpExpressionSerializer.cs` (line ~787)
- **Issue:** The `default` case in `VisitSerializeAsValue` compiles a fresh lambda expression on every invocation: `Expression.Lambda<Func<T, object>>(body, modelParameter).Compile()`. This runs per model evaluation for any `Display.AsValue()` usage in WhenTrue/WhenFalse. `Expression.Compile()` is extremely expensive — involves JIT compilation.
- **Fix:** Added a `static ConditionalWeakTable<Expression, Func<T, object>>` cache on `CSharpExpressionSerializer<T>`. Compiled delegates are cached keyed by expression node reference and reused across evaluations. `ConditionalWeakTable` allows entries to be GC'd when the expression key is collected, avoiding unbounded memory growth.

### 33. 7-12 `Lazy<T>` closure allocations per `HigherOrder*Evaluation` construction
- **Status:** DONE (subsumed by #12)
- **Files:** `HigherOrderProposition/HigherOrderBooleanEvaluation.cs` (7 Lazy fields), `HigherOrderBooleanResultEvaluation.cs` (11 Lazy fields), `HigherOrderPolicyResultEvaluation.cs` (12 Lazy fields)
- **Issue:** Each evaluation instance allocates 7-12 `Lazy<T>` objects with closures capturing the constructor arrays. These types are instantiated inside WhenTrue/WhenFalse delegates, and the consumer typically only accesses 1-3 of the lazy properties. The remaining 4-9 closures are allocated and never invoked.
- **Fix:** Replaced with `_field ??= computation` null-check caching as part of #12 Wave 3. Constructors now only store the source arrays; no closures are allocated upfront.
