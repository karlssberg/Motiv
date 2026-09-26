# Resolving captured values in expression trees: compile, interpret, or reflect

Date: 2026-09-25. Scope: how Motiv should read captured closure values out of `Spec.From(...)` expression
trees. Motiv targets net8.0, net9.0, net10.0 and netstandard2.0.

The Motiv code under study:

- `src/Motiv/ExpressionTreeProposition/TypeExtensions.cs`, `GetConstantExpressionValue` / `IsClosureObject`:
  a type-name heuristic, then `value.GetType().GetField(memberName).GetValue(value)`.
- `src/Motiv/ExpressionTreeProposition/CSharpExpressionSerializer.cs`: the base `VisitSerializeAsValue`
  takes the reflection path and handles only `MemberExpression { Expression: ConstantExpression }`. The
  generic `CSharpExpressionSerializer<T>` compiles every `AsValue` argument to `Func<T, object>`, cached
  in a static `ConditionalWeakTable<Expression, Func<T, object>>`.
- `src/Motiv/ExpressionTreeProposition/ExpressionTreeTransformer.cs`, `TransformSpecExpression`: it
  calls `Expression.Lambda<Func<object>>(Convert(spec)).Compile()()` without caching, once for each
  construction.

Pinned sources: dotnet/runtime `96e94b75`, dotnet/efcore `980c0d2f` (main) and `1579c7c0` (release/8.0),
dotnet/roslyn `155872a7`, dotnet/csharpstandard `107068a0` (draft-v8). All of them were fetched on 2026-09-25.
The numbered references [n] are listed under "Sources" at the end.

---

## 1. Cost: `Compile()` vs `Compile(preferInterpretation: true)` vs `FieldInfo.GetValue`

**What `Compile()` does.** On a runtime that supports dynamic code, `Compile()` calls
`Compiler.LambdaCompiler.Compile(this)`. Otherwise it calls `new Interpreter.LightCompiler().CompileTop(this).CreateDelegate()` [1].
`LambdaCompiler` creates an *anonymously hosted* `DynamicMethod` through
`new DynamicMethod(name, returnType, paramTypes, true)` [2, L74]. It binds the delegate to a
`System.Runtime.CompilerServices.Closure`, which holds the lambda's non-emittable constants in an
`object[] Constants` [2, L265; 3]. Each `Compile()` therefore produces a new method that must be JIT-compiled.

**Magnitudes (primary benchmark).** EF Core issue #29814 ran a BenchmarkDotNet comparison of
compile+invoke against interpret+invoke for delegates that are used only once. The runtime was not stated;
the issue was filed in Dec 2022, so it is likely .NET 7 [4]. Mean times:

| Tree | Compile + invoke once ("EveryTime") | Interpret + invoke once | Invoke only, cached ("Once") compiled / interpreted |
|---|---|---|---|
| depth 1, `Add` | 37,249 ns | 1,637 ns | 0.52 ns / 91.95 ns |
| depth 1, `Call` | 45,410 ns | 1,680 ns | — |
| depth 1000, `Call` | 7,619,107 ns | 175,068 ns | 540 ns / 17,988 ns |

- A one-shot `Compile()` costs roughly **40 µs**. A one-shot interpretation costs roughly **1.6 µs**, about 20–40× cheaper.
- Once a compiled delegate is cached, invoking it costs about **0.5 ns**. An interpreted delegate costs about **90 ns** per invocation.
- EF Core adopted `Compile(preferInterpretation: true)` for one-shot evaluation in EF Core 8 (PR #29815) [4].
- A runtime PR that would have made `AsQueryable` prefer interpretation was **closed unmerged**. jkotas argued
  that the trade-off "depends on the amount of data that the query processes", so changing the default
  would be a breaking change [5]. Interpretation is right for one-shot or low-count execution, not for hot loops.

**Reflection.** `FieldInfo.GetValue` got faster in .NET 9. PR #98199 added an internal `FieldAccessor`
that is cached on the `FieldInfo` [6]. Toub's table [6]:

| Benchmark | .NET 8 | .NET 9 |
|---|---|---|
| `GetInstanceReferenceField` | 29.6 ns | 5.96 ns |
| `GetStaticReferenceField` | 24.8 ns | 1.72 ns |
| `GetStaticValueField` (boxes) | 43.8 ns | 36.0 ns |

.NET 10 shows `FieldInfo.GetValue` at **2.64 ns** against 0.73 ns for `[UnsafeAccessor]` [7]. A value-type
field is boxed by `GetValue`, so each read allocates one box. `PropertyInfo.GetValue` goes through the
method-invoke path. .NET 8 added `MethodInvoker` for cached invocation, with `MethodBase.Invoke` at
32 ns against `MethodInvoker` at 11 ns [8].

**Order of magnitude for a one-shot read of one captured variable:** reflection costs **ns**
(≈2–40 ns depending on TFM). Interpretation costs **low µs**. `Compile()` costs **tens of µs**. These
figures come from different benchmarks on different machines, so compare orders of magnitude only.
The main session's benchmark should confirm them.

**What gets retained.**

- The JIT-compiled code "is reclaimed when the DynamicMethod object is reclaimed" [9].
- A compiled delegate keeps its `DynamicMethod` alive, along with the `Closure.Constants` array [2, L265; 3]. For
  a captured variable, that array holds the compiler-generated display-class instance. **A cached compiled
  delegate therefore roots the captured closure object**, and everything the closure references, for as long
  as the delegate lives.
- An interpreted delegate is a `LightLambda` over interpreter instructions [10]. It does not create a
  per-lambda `DynamicMethod`; creating the delegate goes through a per-delegate-type cache (`_runCache`) [10].
- Reflection retains nothing beyond runtime-owned per-`FieldInfo` caches [6].

## 2. NativeAOT / no-dynamic-code platforms; trimming annotations

- `LambdaExpression.CanCompileToIL` is `RuntimeFeature.IsDynamicCodeSupported` and carries
  `[FeatureGuard(typeof(RequiresDynamicCodeAttribute))]`. `CanInterpret` is always `true` [1, L28–32].
  **When dynamic code is unsupported, `Compile()` falls back to the interpreter; it does not throw** [1, L137–146].
  `Compile(preferInterpretation: true)` interprets unconditionally [1, L155–162, L235–242].
- The Native AOT docs say it directly: "System.Linq.Expressions always use their interpreted form, which is slower
  than runtime generated compiled code". They also list "No runtime code generation, for example,
  System.Reflection.Emit" [11].
- Toub (.NET 8): when dynamic code isn't supported, System.Linq.Expressions "falls back to using an
  interpreter". That turned DI's expression-based factory into "actually a deoptimization", and
  dotnet/runtime#81262 replaced it with a reflection path under `!RuntimeFeature.IsDynamicCodeSupported` [12].
  This is the same pattern Motiv faces.
- **Annotations.** `Expression<TDelegate>.Compile()` / `Compile(bool)` and the generic
  `Expression.Lambda<TDelegate>` factory carry **no** `RequiresDynamicCode`. The *non-generic*
  `Expression.Lambda(body, params …)` overloads, which build a delegate type at runtime, are
  `[RequiresDynamicCode]` [1, L736ff]. All three Motiv call sites use the generic form, so they raise no
  AOT warning. They are slow under AOT, but they work.
- **Trimming.** `Type.GetField(string)` carries `[DynamicallyAccessedMembers(PublicFields)]` on `this` [13].
  `value.GetType().GetField(name)`, as used in `GetClosureObjectField`, is therefore a trim-analysis
  warning (IL2075-class) once `IsTrimmable`/`IsAotCompatible` is enabled. `FieldInfo.GetValue(object)` itself is
  unannotated [14]. The `FieldInfo` already stored in `MemberExpression.Member` is emitted by the compiler as a
  `ldtoken`-based `FieldInfo` (`_bound.FieldInfo(field)`) [15, L828–834], so reading it needs no name lookup and is trim-safe.
- **netstandard2.0.** `Compile(bool)` exists (docs list netstandard-2.0) [16].
  `RuntimeFeature.IsDynamicCodeSupported` does not; it starts at netstandard-2.1 / netcore-3.0 [17]. Motiv does
  not currently set `IsAotCompatible`. Learn recommends enabling it conditionally for net8.0+ [11].
  *Not verified:* whether .NET Framework honours `preferInterpretation` (reference source not retrieved).

## 3. Correctness: what closures look like in expression trees

The authoritative behaviour is Roslyn's `ExpressionLambdaRewriter` [15]:

- A captured `this` becomes `Expression.Constant(this, typeof(C))` (`case BoundKind.ThisReference: return Constant(node)`) [15, L250–252].
- A field access on a *captured frame* (the display-class instance, `FieldSymbol.IsCapturedFrame`) becomes
  `Expression.Constant(frame)` [15, L214–220]. Any other field access becomes
  `Expression.Field(receiver, fieldInfo)` with a `null` receiver when the field is static [15, L828–834].
- So `x` captured from a local is `MemberExpression(FieldInfo x, Constant(<>c__DisplayClassN_M))`.
  A variable from an *outer* scope is reached through a frame-pointer field: the inner display class holds a field
  named by `MakeLambdaDisplayLocalName`, which is `"CS$<>8__locals" + n` [18, L282–285; 19, L87–88]. The tree is
  therefore `Member(Member(Constant(inner), CS$<>8__locals1), x)`. A captured `this` inside a frame is the
  field `<>4__this` [18, L338–341].
- Display-class fields are `public`, non-readonly and instance [19, L26–31]. Most keep the user's variable name, but
  pattern variables in switch sections get `<name>5__N` [19, L76–113]. The display class is a `private`,
  `sealed` nested type [20, L92, L156] marked `[CompilerGenerated]` [20, L60–73].
- **Guarantees.** None of this is specified. The C# standard's closure-lowering section is "a possible
  implementation … by no means a mandated implementation", and it "only briefly mentions conversions to
  expression trees, as their exact semantics are outside the scope of this specification" [21, §12.21.8].
  Names such as `<>c__DisplayClass` come from Roslyn internals (`GeneratedNames`, `GeneratedNameKind`) [18, 22].
  Matt Warren raised the same objection in 2007: "How do I identify a compiler generated type? By name? What
  if the c# compiler changes how they name them? … Can I just recognize any member access against a
  constant node and evaluate it by hand using reflection? Maybe." [23]
- **What EF Core does instead.** It detects a closure as `Type.Attributes.HasFlag(NestedPrivate) &&
  IsDefined(typeof(CompilerGeneratedAttribute))`, **not by name**, and only to decide whether a value becomes a
  *parameter* or a *constant* [24, L600–615; 25, L757]. It **evaluates any member chain generically**:
  it recurses to the root, `ConstantExpression → .Value`, then applies `FieldInfo.GetValue` /
  `PropertyInfo.GetValue(instance)` at each hop. It unwraps nullable-only `Convert`. On any exception, or any other
  node type, it falls back to `Lambda<Func<object>>(Convert(e, object)).Compile(preferInterpretation: true).Invoke()`
  [24, L2249–2296; 25, L504–577]. This covers static fields (`GetValue(null)`), captured `this`, nested frames and
  properties, and needs no name heuristic.

**Consequences for Motiv's heuristic** (from reading the code; not reproduced by a test):
`IsClosureObject` = `Name.StartsWith("<>") || Name.Contains("DisplayClass")`.
- It gives a false positive for any user type whose name contains "DisplayClass".
- It gives a false negative for captured `this`. `Display.AsValue(_field)` inside an instance method is
  `Member(_field, Constant(this))`. The type is not a closure, so `GetConstantExpressionValue` returns
  **the enclosing object itself** rather than the field value. The same happens for `this.Property`.
- Re-looking the field up by name is redundant, because `memberExpression.Member` already *is* the `FieldInfo`/`PropertyInfo`.
- Nested frames (`CS$<>8__locals1.x`) and static members do not match the one-hop pattern in the base
  `VisitSerializeAsValue`, so they fall to `Visit(node)` and serialize as text rather than as a value.

## 4. Caching compiled delegates

- **`ConditionalWeakTable` semantics** [26]: keys are compared by `ReferenceEquals`, and `GetHashCode`
  overrides are not used. The key is not kept alive by membership, "even if it can be reached directly from a value
  stored in the table". The implementation stores each entry in a `DependentHandle`: a weak primary and a strong
  secondary that lives only while the primary does [27, L546]. So a cached `Func<T,object>` whose `Closure` roots the
  display class, which in turn is reachable from the key tree, **does not leak**. The entry dies with the `Expression`
  key. Reads are lock-free and writes take a lock [27, L554; L75ff]. The factory "may invoke createValueCallback
  multiple times" under a race [27, L292], so a delegate may be compiled twice; this is harmless.
- **Reference keys only hit for the same tree instance.** Roslyn lowers an expression-lambda conversion to a sequence of
  `Expression.*` factory calls (`_bound.StaticCall(Expression__Constant …)`) that run each time the conversion is
  evaluated [15, L1254–1263]. The standard likewise says that evaluating the conversion "produces an object structure"
  [21, conversions §10.7.3]. So each `Spec.From(...)` call builds a fresh tree. Motiv's reference-keyed cache
  helps only when one spec instance is evaluated repeatedly, which is the normal case.
- **Structural keys (EF Core).** The query cache keys on `ExpressionEqualityComparer` [28, L75–89]. EF Core first
  funcletizes every captured value *into a parameter*, so the key is value-independent and one compiled query
  serves every closure value. Its per-extraction dedupe `_parameterizedValues` is also structurally keyed [24, L77].
  A structural key is only safe once the closure constants have been lifted out. Otherwise two trees with different
  display-class instances compare unequal (`CompareConstant` uses `Equals`) [29, L359–366], or they alias stale values.
- **Memory.** Each compiled delegate pins one anonymously hosted `DynamicMethod` and its JIT code, plus its `Closure`
  constants, until the delegate is collected [2, 9]. The cost scales with the number of distinct `AsValue` nodes ×
  distinct `T`, because the static cache is per closed generic.
- **When caching pays.** Compiling costs about 40 µs [4], and a reflection hop costs about 2–30 ns [6, 7]. For a
  **parameter-free** subtree (a pure closure read), compile-and-cache breaks even with plain reflection only after
  roughly 10³–10⁴ evaluations of that node. For a subtree that **references the model parameter**, reflection
  cannot help: it needs compile-and-cache (hot path) or an interpreted delegate (cold path). Under AOT, "compiled"
  means interpreted, so a cached delegate costs about 90 ns per call [4, 11].

## 5. Which nodes to pre-evaluate ("funcletize")

- **The classic rule** (Warren's `Nominator`): a node is evaluatable if none of its descendants is a
  `ParameterExpression`. Evaluation is `Expression.Lambda(e).Compile()` plus `DynamicInvoke`, and constants are
  left as they are [23].
- **EF Core's refinements** [24, 30]:
  - Parameters are never evaluatable roots (L1347–1375).
  - Block, Loop, Goto, Try, Switch, Dynamic and similar node types are rejected outright (L1753ff).
  - `IQueryable` constants are not evaluated (L611).
  - Static members *are* evaluatable, but they are treated as captured, so they become a parameter rather than
    being baked in, unless the field is `IsInitOnly` (L862–870).
  - `EvaluatableExpressionFilter` refuses **non-deterministic** members: `DateTime.Now/UtcNow/Today`,
    `DateTimeOffset.Now/UtcNow`, `Guid.NewGuid()`, `Random.Next(...)` ("result varies based on time of running
    the query", EF issue #2069) [30, L9–31, L58–79].
  - `New`, `NewArray`, `MemberInit`, `ListInit`, `Convert`, `Call` with evaluatable arguments, and conditionals are
    evaluatable when all their children are (visitor overrides L1209–1659).
- **For Motiv, the risk is baking in, not evaluating.** Baking a value in at construction snapshots it. That is right
  for `TransformSpecExpression`, where a captured *spec* is structural. It is wrong for a value that is meant to be read
  live at evaluation time, such as `AsValue(capturedCounter)` where the variable mutates. A captured display-class
  field is mutable (non-readonly [19, L30]). A snapshot would change semantics, whereas reading through the
  `MemberExpression` at evaluation time, by reflection or by a delegate, stays live.

---

## 6. Measurements (this repo, 2026-09-25)

Apple M2, macOS 26.6, BenchmarkDotNet 0.15.8 `--job medium`. Source:
`src/examples/Motiv.Benchmark/ConstantResolutionBenchmarks.cs`
(`dotnet run -c Release -f net8.0 -- --filter '*ConstantResolution*' '*SpecFromConstruction*' --runtimes net8.0 net10.0`;
net8 needs `DOTNET_ROOT`/`--cli` pointed at a muxer that has the 8.0 runtime).

**Mechanism — resolving a captured `threshold` (one-shot) and `config.Limit` (field→property chain):**

| Method | net8.0 | net10.0 | Allocated |
|---|--:|--:|--:|
| Reflection, by name (today's `GetConstantExpressionValue`) | 54.6 ns | 19.6 ns | 24 B |
| Reflection, `MemberInfo` taken from the node | 37.7 ns | 9.9 ns | 24 B |
| Chain via reflection (field, then property) | 45.4 ns | 22.9 ns | 24 B |
| `Compile(preferInterpretation: true)` + invoke | 794 ns | 704 ns | 1,224 B |
| `Compile()` + invoke | 36,378 ns | 34,488 ns | 4,311 B |
| Chain via `Compile()` + invoke | 41,257 ns | 38,917 ns | 4,343 B |

(The 24 B everywhere is boxing the `int` to `object`.)

**Mechanism — per-evaluation read (what `CSharpExpressionSerializer<T>` does on every `Evaluate`):**

| Method | net8.0 | net10.0 | Allocated (net8 / net10) |
|---|--:|--:|--:|
| `ConditionalWeakTable` lookup + cached delegate (today) | 12.9 ns | 8.3 ns | **88 B** / 24 B |
| Delegate held directly (no lookup) | 3.0 ns | 3.1 ns | 24 B |
| Reflection each call | 37.8 ns | 9.8 ns | 24 B |
| Compile each call (no cache) | 37,396 ns | 35,342 ns | 4,911 B |

Today's CWT path allocates an extra 64 B per `AsValue` node per evaluation on net8: the factory lambda passed
to `GetValue` captures `modelParameter`, so a delegate is allocated on every call, hit or miss. net10's
escape analysis removes it.

**Retention** (`dotnet run retention.cs`, 10,000 distinct nodes): a compiled delegate retains ~1.0 KB of
managed heap (JIT) / ~0.9 KB (interpreted), plus native code for the JIT case that `GC.GetTotalMemory` does not
see. Keyed by node in a `ConditionalWeakTable`, **0 of 10,000** delegates survived once their nodes were
collected — the cache does not leak; it is bounded by the number of live propositions, not by evaluations.

**End-to-end, public API:**

| Scenario | net8.0 | net10.0 | Allocated |
|---|--:|--:|--:|
| `Spec.From(n => n >= Display.AsValue(threshold)).Create(..)` | 39.8 µs | 36.1 µs | 7.4 KB |
| `Spec.From(xs => xs.All(capturedSpec)).Create(..)` | 36.0 µs | 31.6 µs | 7.1 KB |
| … same, with a reflection fast path prototyped in `TransformSpecExpression` | — | **1.46 µs** | **3.0 KB** |
| `Evaluate(7).Assertions` on the `AsValue` spec | 407 ns | 311 ns | 1.9–2.0 KB |

The prototype (walk `Constant`/`Member(FieldInfo)` by reflection, else fall back to the existing compile) makes
captured-spec construction **~21× faster** and halves its allocation. It was reverted; it is evidence, not a change.

**Correctness probe** — the construction-time (non-generic) serializer vs the evaluation-time (generic) one, for
`n => n == Display.AsValue(x)`, all values 5:

| `x` | construction-time text (used in `Justification`) | evaluation-time text |
|---|---|---|
| captured local `threshold` | `n == 5` | `n == 5` |
| `cfg.Limit` (captured object's property) | `n == cfg.Limit` | `n == 5` |
| static field `StaticLimit` | `n == StaticLimit` | `n == 5` |
| `this._instanceLimit` / `this.InstanceProp` | **`n == Motiv.Tests.Scratch.ConstantResolutionProbe`** | `n == 5` |
| `GetLimit()`, `arr[0]`, `threshold + 0` | source text | `n == 5` |

The `this` rows are a user-visible bug: `IsClosureObject` rejects the enclosing class, so the whole object's
`ToString()` is printed. It shows up in `Justification`'s statement line, e.g.
`(int n) => n == Motiv.Tests.Scratch.ConstantResolutionProbe == true`. The other rows are an inconsistency:
the two serializers disagree on anything but a bare captured local.

---

## Implications for Motiv

1. **Replace the name heuristic with EF's member-chain evaluator.** Walk `Member*` down to a `Constant`, or to a
   `null` static root. Apply `((FieldInfo|PropertyInfo)member).GetValue(instance)` at each hop and unwrap
   `Convert`. This fixes captured `this`, nested frames and statics. It also removes the trim-unsafe
   `GetType().GetField(name)`. If `[CompilerGenerated]` + `NestedPrivate` is ever needed, use that, never the name.
2. **`TransformSpecExpression`:** use that fast path, falling back to `Compile(preferInterpretation: true)`. This is
   one-shot at construction: the drop is about 40 µs → ns (member chain) or about 1.6 µs (anything else), and it
   pins no `DynamicMethod`. Snapshotting is correct here.
3. **`CSharpExpressionSerializer<T>`:** keep compile + cache only for subtrees that reference the model parameter.
   Resolve parameter-free member chains by reflection on every call: it stays live, and costs 10–38 ns against a
   ~300–400 ns `Evaluate`+`Assertions` (§6), while skipping a ~36 µs compile and ~1 KB retained per node. The measured
   cheapest option for parameter-dependent nodes is to compile once at construction and hold the delegate on the
   proposition (3 ns, no lookup) rather than look it up in a static CWT (8–13 ns, plus 64 B/call on net8). If the
   CWT stays, stop allocating the factory closure on every lookup (`TryGetValue` first).
4. **Keep the two serializers consistent.** Route the construction-time `VisitSerializeAsValue` through the same
   evaluator so `Justification` and `Assertions` agree; that also fixes the captured-`this` bug in §6.
5. Under NativeAOT, every `Compile()` is already interpreted [1, 11]. The reflection path is the only fast one there,
   which is the same conclusion as dotnet/runtime#81262 [12].

## Sources

1. dotnet/runtime `LambdaExpression.cs` — https://github.com/dotnet/runtime/blob/96e94b7583745b942234f5e5b8c8cf622784fa07/src/libraries/System.Linq.Expressions/src/System/Linq/Expressions/LambdaExpression.cs#L28-L32 (also #L137-L162, #L217-L242, #L736)
2. dotnet/runtime `LambdaCompiler.cs` — https://github.com/dotnet/runtime/blob/96e94b7583745b942234f5e5b8c8cf622784fa07/src/libraries/System.Linq.Expressions/src/System/Linq/Expressions/Compiler/LambdaCompiler.cs#L74 (and #L265)
3. dotnet/runtime `Closure.cs` — https://github.com/dotnet/runtime/blob/96e94b7583745b942234f5e5b8c8cf622784fa07/src/libraries/System.Linq.Expressions/src/System/Runtime/CompilerServices/Closure.cs#L14-L31
4. EF Core issue #29814, "Use interpreted dynamic delegates when they're invoked just once" (benchmarks; fixed by PR #29815) — https://github.com/dotnet/efcore/issues/29814
5. dotnet/runtime PR #79698 "Prefer interpreted execution for AsQueryable" (closed; jkotas comments) — https://github.com/dotnet/runtime/pull/79698
6. Stephen Toub, *Performance Improvements in .NET 9*, Reflection section (dotnet/runtime#98199, #92512) — https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-9/
7. Stephen Toub, *Performance Improvements in .NET 10*, Reflection section (`FieldInfo.GetValue` vs `[UnsafeAccessor]`) — https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/
8. Stephen Toub, *Performance Improvements in .NET 8*, Reflection section (`MethodInvoker`, dotnet/runtime#88415) — https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-8/
9. Learn, `DynamicMethod` class remarks ("reclaimed when the DynamicMethod object is reclaimed"; anonymously hosted) — https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.dynamicmethod (source xml @ dotnet-api-docs-temp `81138006`)
10. dotnet/runtime `Interpreter/LightLambda.cs` — https://github.com/dotnet/runtime/blob/96e94b7583745b942234f5e5b8c8cf622784fa07/src/libraries/System.Linq.Expressions/src/System/Linq/Expressions/Interpreter/LightLambda.cs#L195-L282
11. Learn, *Native AOT deployment overview* (limitations; `IsAotCompatible` guidance) — https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/ (docs @ `45d22543`)
12. Toub, *Performance Improvements in .NET 8*, Native AOT section (ActivatorUtilities, dotnet/runtime#81262) — https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-8/
13. dotnet/runtime `Type.cs` `GetField(string)` annotation — https://github.com/dotnet/runtime/blob/96e94b7583745b942234f5e5b8c8cf622784fa07/src/libraries/System.Private.CoreLib/src/System/Type.cs#L219-L220
14. dotnet/runtime `FieldInfo.cs` — https://github.com/dotnet/runtime/blob/96e94b7583745b942234f5e5b8c8cf622784fa07/src/libraries/System.Private.CoreLib/src/System/Reflection/FieldInfo.cs#L64
15. dotnet/roslyn `ExpressionLambdaRewriter.cs` — https://github.com/dotnet/roslyn/blob/155872a7df036ee4da7b9bf78504d0f4f6c82c52/src/Compilers/CSharp/Portable/Lowering/ClosureConversion/ExpressionLambdaRewriter.cs#L214-L252 (also #L828-L834, #L1254-L1263)
16. Learn, `Expression<TDelegate>.Compile` (overloads, applies-to) — https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression-1.compile
17. Learn, `RuntimeFeature.IsDynamicCodeSupported` — https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.runtimefeature.isdynamiccodesupported
18. dotnet/roslyn `GeneratedNames.cs` — https://github.com/dotnet/roslyn/blob/155872a7df036ee4da7b9bf78504d0f4f6c82c52/src/Compilers/CSharp/Portable/Symbols/Synthesized/GeneratedNames.cs#L43-L54 (also #L282-L285, #L338-L341)
19. dotnet/roslyn `LambdaCapturedVariable.cs` — https://github.com/dotnet/roslyn/blob/155872a7df036ee4da7b9bf78504d0f4f6c82c52/src/Compilers/CSharp/Portable/Lowering/ClosureConversion/LambdaCapturedVariable.cs#L26-L31 (also #L76-L113)
20. dotnet/roslyn `SynthesizedContainer.cs` — https://github.com/dotnet/roslyn/blob/155872a7df036ee4da7b9bf78504d0f4f6c82c52/src/Compilers/CSharp/Portable/Symbols/Synthesized/SynthesizedContainer.cs#L60-L73 (also #L92, #L156)
21. C# standard (draft-v8) §12.21.8 *Implementation example*, §10.7.3 — https://github.com/dotnet/csharpstandard/blob/107068a0fee88b13e9c46ff64f98343ff29ff8ee/standard/expressions.md#12218-implementation-example and https://github.com/dotnet/csharpstandard/blob/107068a0fee88b13e9c46ff64f98343ff29ff8ee/standard/conversions.md#1073-evaluation-of-lambda-expression-conversions-to-expression-tree-types
22. dotnet/roslyn `GeneratedNameKind.cs` — https://github.com/dotnet/roslyn/blob/155872a7df036ee4da7b9bf78504d0f4f6c82c52/src/Compilers/CSharp/Portable/Symbols/Synthesized/GeneratedNameKind.cs
23. Matt Warren, *LINQ: Building an IQueryable Provider – Part III* (Evaluator / Nominator), 2007 — https://learn.microsoft.com/en-us/archive/blogs/mattwar/linq-building-an-iqueryable-provider-part-iii
24. dotnet/efcore `ExpressionTreeFuncletizer.cs` — https://github.com/dotnet/efcore/blob/980c0d2f8a957a5a2597c3b0d869fcfadfb41053/src/EFCore/Query/Internal/ExpressionTreeFuncletizer.cs#L2249-L2296 (also #L77, #L600-L615, #L860-L870, #L1347-L1375, #L1753)
25. dotnet/efcore release/8.0 `ParameterExtractingExpressionVisitor.cs` — https://github.com/dotnet/efcore/blob/1579c7c0295810a36db68f3ff9d087aec8cc59d8/src/EFCore/Query/Internal/ParameterExtractingExpressionVisitor.cs#L504-L577 (also #L757)
26. Learn, `ConditionalWeakTable<TKey,TValue>` — https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.conditionalweaktable-2
27. dotnet/runtime `ConditionalWeakTable.cs` — https://github.com/dotnet/runtime/blob/96e94b7583745b942234f5e5b8c8cf622784fa07/src/libraries/System.Private.CoreLib/src/System/Runtime/CompilerServices/ConditionalWeakTable.cs#L546-L555 (also #L292)
28. dotnet/efcore `CompiledQueryCacheKeyGenerator.cs` — https://github.com/dotnet/efcore/blob/980c0d2f8a957a5a2597c3b0d869fcfadfb41053/src/EFCore/Query/CompiledQueryCacheKeyGenerator.cs#L75-L89
29. dotnet/efcore `ExpressionEqualityComparer.cs` — https://github.com/dotnet/efcore/blob/980c0d2f8a957a5a2597c3b0d869fcfadfb41053/src/EFCore/Query/ExpressionEqualityComparer.cs#L359-L366
30. dotnet/efcore `EvaluatableExpressionFilter.cs` — https://github.com/dotnet/efcore/blob/980c0d2f8a957a5a2597c3b0d869fcfadfb41053/src/EFCore/Query/EvaluatableExpressionFilter.cs#L9-L79

## Addendum: implemented (2026-09-26)

Recommendations 1–4 are implemented test-first; see `test/Motiv.Tests/ExpressionTreeAsValueResolutionTests.cs`.

- `CapturedValueReader` walks a member chain by reflection, using the `MemberInfo` on each node and rooted in a
  constant or a static member. `TryEvaluate` adds an interpreted fallback for parameter-free expressions, and
  any failure there leaves the source text in place. It replaces `GetConstantExpressionValue` and the
  `DisplayClass` name heuristic.
- Construction-time `AsValue` text now matches evaluation-time text for captured `this` members, member chains,
  statics, enclosing-scope locals, method calls and arithmetic. One visible change: a captured enum now prints as
  `Suit.Hearts` rather than `Hearts`, matching the assertion line beneath it (the Poker example's expectation is
  updated).
- Compiler-generated closure hops (`CS$<>8__locals1`) are no longer printed as part of a captured variable's
  name.
- `TransformSpecExpression` reads a captured spec by reflection, and otherwise interprets the expression.
- The per-evaluation path reads parameter-free member chains live by reflection. It compiles and caches the
  rest, and a cache hit no longer allocates the factory delegate.

After, same machine, `--job medium`:

| Scenario | net8.0 before → after | net10.0 before → after |
|---|--:|--:|
| `Spec.From(xs => xs.All(capturedSpec))` | 36.0 µs / 7.2 KB → **1.85 µs / 3.0 KB** | 31.6 µs / 7.1 KB → **2.3 µs / 3.0 KB** |
| `Spec.From(… AsValue(threshold))` construction | 39.8 µs → 41.6 µs (±5.2, noise) | 36.1 µs → 35.6 µs |
| `Evaluate(7).Assertions`, `AsValue(threshold)` | 407 ns / 2.0 KB → 432 ns / 1.94 KB | 311 ns → 320 ns (±17) |

The per-evaluation trade-off shows up on net8. Reading a member chain live by reflection costs about 25 ns more
than invoking a cached compiled delegate, because `FieldInfo.GetValue` is about 38 ns there against about 10 ns
on net10. In return it saves 64 B per evaluation, a ~36 µs compile on first use, and ~1 KB retained per `AsValue`
node.
