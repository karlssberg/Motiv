# Motiv Analyzer & Code Fix Roadmap

## Overview

This document outlines potential enhancements to the Motiv.Analyzer and Motiv.CodeFix projects. The current implementation successfully detects "Boolean Blindness" by converting binary expressions into Motiv Spec propositions. The proposed enhancements focus on:

1. **Promoting proper Spec composition patterns**
2. **Detecting and extracting repeated domain logic**
3. **Teaching idiomatic Motiv API usage**
4. **Improving semantic clarity and maintainability**

---

## Current State

### Existing Functionality
- **Analyzer:** Detects binary expressions (comparisons, logical operators) - `MOTIV0001`
- **Code Fix:** Converts expressions to `Spec<T>` with `.IsSatisfiedBy()` invocations
- **Coverage:** Simple and complex boolean expressions
- **Smart Filtering:** Ignores expressions already inside `Spec.Build()` lambdas

### Limitations
- Only handles expression-level transformations
- Generic "Proposition" naming doesn't capture domain semantics
- Doesn't detect higher-level composition anti-patterns
- Doesn't leverage full Motiv API (collection methods, policies, higher-order specs)

---

## Proposed Code Fixes

### Priority 1: High-Value, High-Impact

#### 1. Extract Repeated Boolean Patterns to Reusable Spec
**Diagnostic ID:** `MOTIV0002`
**Severity:** Info
**Title:** "Extract repeated boolean pattern to reusable Spec"

**Description:**
Analyzes the semantic model to detect the same boolean expression pattern used in multiple locations within a class or namespace. Suggests extracting to a named, reusable spec.

**Example:**

```csharp
// Detected pattern (used 3+ times):
public bool CanVote(Person p) => p.Age >= 18 && p.HasCitizenship;
public bool CanDrive(Person p) => p.Age >= 18 && p.HasLicense;
private bool CheckEligibility() => age >= 18 && hasCitizenship;
```

**Suggested Fix:**
```csharp
private static readonly SpecBase<Person> IsAdult =
    Spec.Build((Person p) => p.Age >= 18)
        .Create("is an adult");

public bool CanVote(Person p) =>
    IsAdult.AndAlso(Spec.Build((Person p) => p.HasCitizenship)
                        .Create("has citizenship"))
    .IsSatisfiedBy(p).Satisfied;
```

**Benefits:**
- Captures domain concepts
- DRY principle enforcement
- Testable, reusable specifications
- Promotes composition over duplication

**Implementation Complexity:** Medium
**Estimated Effort:** 2-3 days

---

#### 2. Suggest Spec Composition Over Multiple IsSatisfiedBy Calls
**Diagnostic ID:** `MOTIV0003`
**Severity:** Info
**Title:** "Compose specs instead of multiple satisfaction checks"

**Description:**
Detects multiple `.IsSatisfiedBy()` calls on the same model combined with boolean operators. Suggests composing the specs first.

**Example:**

```csharp
// Detected anti-pattern:
if (isAdult.IsSatisfiedBy(person).Satisfied &&
    hasLicense.IsSatisfiedBy(person).Satisfied &&
    !hasDUI.IsSatisfiedBy(person).Satisfied)
{
    AllowDriving();
}
```

**Suggested Fix:**
```csharp
private static readonly SpecBase<Person> CanDrive =
    isAdult.AndAlso(hasLicense).AndAlso(hasDUI.Not());

if (CanDrive.IsSatisfiedBy(person).Satisfied)
{
    AllowDriving();
}
```

**Benefits:**
- Creates named domain concepts
- Improves performance (single satisfaction check)
- Better composition semantics
- Proper metadata propagation

**Implementation Complexity:** Medium
**Estimated Effort:** 3-4 days

---

#### 3. Context-Aware Semantic Naming
**Enhancement to:** Existing `MOTIV0001` code fix
**Priority:** High

**Description:**
Enhance the current code fix to suggest semantically meaningful names instead of generic "Proposition" by analyzing:
- Containing method/property names
- Variable names in expression
- Expression pattern matching (age >= 18 → "IsAdult")
- Surrounding type context

**Example:**

```csharp
// Current fix generates:
public class Proposition() : Spec<int> { ... }

// Enhanced fix suggests (with options):
// Option 1: IsAdultSpec (from pattern: age >= 18)
// Option 2: IsVotingEligibleSpec (from method: IsEligibleForVoting)
// Option 3: MeetsAgeRequirementSpec (from context)
```

**Implementation Approach:**
1. Build pattern library (age >= 18 → IsAdult, x > 0 → IsPositive)
2. Analyze method name tokens (IsEligibleForVoting → VotingEligibility)
3. Extract variable name semantics (customerAge → Customer + Age)
4. Provide multiple naming options in code fix

**Benefits:**
- Self-documenting code
- Better spec discovery
- Captures domain language
- Reduces cognitive load

**Implementation Complexity:** Medium-High
**Estimated Effort:** 4-5 days

---

### Priority 2: Medium-Value, Practical Improvements

#### 4. Detect Manual Aggregation → Suggest Collection Methods
**Diagnostic ID:** `MOTIV0004`
**Severity:** Info
**Title:** "Use collection composition methods"

**Description:**
Detects manual aggregation of specs with LINQ and suggests using Motiv's semantic collection methods.

**Example:**

```csharp
// Detected:
var combined = specs.Aggregate((left, right) => left & right);
var anyTrue = specs.Any(s => s.IsSatisfiedBy(model).Satisfied);
```

**Suggested Fix:**
```csharp
var combined = specs.AndTogether();
var anyTrue = specs.AnyTrue(model);
```

**Patterns to Detect:**
- `Aggregate((l, r) => l & r)` → `AndTogether()`
- `Aggregate((l, r) => l | r)` → `OrTogether()`
- `Any(s => s.IsSatisfiedBy(...).Satisfied)` → `AnyTrue(...)`
- `All(s => s.IsSatisfiedBy(...).Satisfied)` → `AllTrue(...)`

**Benefits:**
- Idiomatic API usage
- Better readability
- Proper metadata composition
- Performance optimization (short-circuit methods)

**Implementation Complexity:** Low-Medium
**Estimated Effort:** 2-3 days

---

#### 5. Suggest AndAlso/OrElse for Performance
**Diagnostic ID:** `MOTIV0005`
**Severity:** Info
**Title:** "Use short-circuit evaluation"

**Description:**
Detects composition patterns where short-circuit evaluation would be beneficial and suggests `AndAlso`/`OrElse`.

**Heuristics:**
1. Precondition pattern: null checks before validation
2. Expensive operation pattern: database/IO operations
3. Early-exit pattern: validation gates

**Example:**

```csharp
// Detected pattern (null check + expensive check):
var isValid = notNull.And(existsInDatabase).And(hasValidFormat);

// Suggested fix:
var isValid = notNull.AndAlso(existsInDatabase).AndAlso(hasValidFormat);
```

**Benefits:**
- Performance optimization
- Semantic clarity about evaluation order
- Prevents unnecessary computation
- Documents dependencies

**Implementation Complexity:** Medium
**Estimated Effort:** 3-4 days

**Challenges:**
- Detecting "expensive" operations requires heuristics
- May need user configuration for sensitivity

---

#### 6. Suggest Spec.From() for Multi-Assertion Patterns
**Enhancement to:** Existing `MOTIV0001` code fix
**Priority:** Medium

**Description:**
When an expression contains multiple independent assertions (3+), offer `Spec.From()` as an alternative to `Spec.Build()`.

**Example:**

```csharp
// Expression with 3+ independent assertions:
return n >= 1 && n <= 10 && n % 2 == 0;

// Current fix: Spec.Build()
// Suggested alternative: Spec.From() with auto-decomposition
var spec = Spec.From((int n) => n >= 1 && n <= 10 && n % 2 == 0)
               .Create("in range and even");
```

**Benefits:**
- Automatic assertion decomposition
- Individual assertion tracking in results
- Better explanation generation
- Clearer failure reasons

**Implementation Complexity:** Low
**Estimated Effort:** 1-2 days

---

### Priority 3: Advanced Patterns

#### 7. Policy Pattern for Strategy Selection
**Diagnostic ID:** `MOTIV0006`
**Severity:** Info
**Title:** "Convert to Policy pattern for strategy selection"

**Description:**
Detects if/else-if chains evaluating conditions on the same model with different return values. Suggests Policy pattern.

**Example:**

```csharp
// Detected pattern:
public HandRank EvaluateHand(Hand hand)
{
    if (isRoyalFlush.IsSatisfiedBy(hand).Satisfied)
        return HandRank.RoyalFlush;
    else if (isStraightFlush.IsSatisfiedBy(hand).Satisfied)
        return HandRank.StraightFlush;
    else if (isFourOfKind.IsSatisfiedBy(hand).Satisfied)
        return HandRank.FourOfKind;
    // ... more conditions
}
```

**Suggested Fix:**
```csharp
public class PokerHandPolicy() : Policy<Hand, HandRank>(
    new PolicyBase<Hand, HandRank>[]
    {
        new IsRoyalFlushRule(),
        new IsStraightFlushRule(),
        new IsFourOfKindRule(),
        // ...
    }.OrElseTogether());

public HandRank EvaluateHand(Hand hand) =>
    new PokerHandPolicy().Evaluate(hand).Value;
```

**Benefits:**
- Declarative strategy pattern
- Proper metadata and explanations
- Testable individual rules
- Clear evaluation order

**Implementation Complexity:** High
**Estimated Effort:** 5-7 days

**Challenges:**
- Complex pattern detection
- Generating rule classes
- Determining return value types

---

#### 8. Detect Inverted Logic → Suggest .Not()
**Diagnostic ID:** `MOTIV0007`
**Severity:** Info
**Title:** "Use spec negation instead of inverting result"

**Description:**
Detects negation of `.IsSatisfiedBy().Satisfied` and suggests composing with `.Not()`.

**Example:**

```csharp
// Detected:
if (!isExpired.IsSatisfiedBy(token).Satisfied)
{
    ProcessToken();
}

// Suggested:
var isValid = isExpired.Not();
if (isValid.IsSatisfiedBy(token).Satisfied)
{
    ProcessToken();
}
```

**Benefits:**
- Positive domain concepts
- Proper metadata negation
- Better readability
- Self-documenting code

**Implementation Complexity:** Low
**Estimated Effort:** 1-2 days

---

#### 9. Higher-Order Spec for Collection Validation
**Diagnostic ID:** `MOTIV0008`
**Severity:** Info
**Title:** "Use higher-order spec for collection validation"

**Description:**
Detects LINQ collection operations that could use Motiv's higher-order spec capabilities.

**Example:**

```csharp
// Detected:
var allValid = items.All(item => isValid.IsSatisfiedBy(item).Satisfied);
var hasAnyExpired = items.Any(item => isExpired.IsSatisfiedBy(item).Satisfied);

// Suggested:
var allValid = isValid.AsAllSatisfy().IsSatisfiedBy(items).Satisfied;
var hasAnyExpired = isExpired.AsAnySatisfy().IsSatisfiedBy(items).Satisfied;
```

**Benefits:**
- Rich explanations (which items failed)
- Proper metadata aggregation
- Consistent API usage
- Better debugging

**Implementation Complexity:** Medium
**Estimated Effort:** 3-4 days

---

## Implementation Phases

### Phase 1: Foundation (Weeks 1-2)
**Goal:** Enhance existing functionality

1. **Context-Aware Semantic Naming** (#3)
   - Pattern library for common expressions
   - Method/variable name analysis
   - Multiple naming suggestions

2. **Spec.From() Alternative** (#6)
   - Detect multi-assertion patterns
   - Offer as code fix option
   - Documentation/tooltips

**Deliverables:**
- Enhanced MOTIV0001 code fix
- Pattern recognition library
- Unit tests

---

### Phase 2: Composition Patterns (Weeks 3-5)
**Goal:** Teach proper spec composition

1. **Composition Over Multiple Calls** (#2)
   - Semantic model analysis
   - Detect multiple `.IsSatisfiedBy()` on same model
   - Generate composed spec

2. **Collection Methods** (#4)
   - LINQ pattern detection
   - Suggest semantic alternatives
   - Support all collection methods

3. **Inverted Logic** (#8)
   - Simple pattern, quick win
   - Detect `!...IsSatisfiedBy().Satisfied`
   - Suggest `.Not()` composition

**Deliverables:**
- MOTIV0003 analyzer + code fix
- MOTIV0004 analyzer + code fix
- MOTIV0007 analyzer + code fix
- Integration tests

---

### Phase 3: Advanced Analysis (Weeks 6-8)
**Goal:** Detect cross-method patterns

1. **Extract Repeated Patterns** (#1)
   - Cross-method expression analysis
   - Similarity detection algorithm
   - Suggest extraction with naming

2. **Short-Circuit Evaluation** (#5)
   - Heuristic development for expensive operations
   - Pattern recognition for preconditions
   - User configuration support

**Deliverables:**
- MOTIV0002 analyzer + code fix
- MOTIV0005 analyzer + code fix
- Configuration schema
- Performance benchmarks

---

### Phase 4: Strategic Patterns (Weeks 9-10)
**Goal:** Advanced architectural patterns

1. **Policy Pattern Detection** (#7)
   - Complex pattern matching
   - Rule class generation
   - Policy class scaffolding

2. **Higher-Order Specs** (#9)
   - LINQ pattern detection
   - Collection spec suggestions
   - Rich explanation support

**Deliverables:**
- MOTIV0006 analyzer + code fix
- MOTIV0008 analyzer + code fix
- Advanced pattern documentation

---

## Success Metrics

### Adoption Metrics
- Number of diagnostics triggered per codebase
- Code fix acceptance rate
- User satisfaction surveys

### Quality Metrics
- False positive rate < 5%
- Code fix correctness (automated tests)
- Build/compile success after fixes

### Education Metrics
- Developer understanding of Motiv patterns
- Reduction in manual composition anti-patterns
- Increase in spec reuse

---

## Technical Considerations

### Architecture

```
Motiv.Analyzer/
├── Analyzers/
│   ├── BooleanBlindnessAnalyzer.cs (existing MOTIV0001)
│   ├── RepeatedPatternAnalyzer.cs (MOTIV0002)
│   ├── CompositionAnalyzer.cs (MOTIV0003)
│   ├── CollectionMethodAnalyzer.cs (MOTIV0004)
│   ├── ShortCircuitAnalyzer.cs (MOTIV0005)
│   ├── PolicyPatternAnalyzer.cs (MOTIV0006)
│   ├── NegationAnalyzer.cs (MOTIV0007)
│   └── HigherOrderSpecAnalyzer.cs (MOTIV0008)
├── PatternDetection/
│   ├── ExpressionSimilarityComparer.cs
│   ├── SemanticPatternMatcher.cs
│   └── PerformanceHeuristics.cs
└── Naming/
    ├── PatternLibrary.cs
    ├── SemanticNameSuggester.cs
    └── ContextAnalyzer.cs

Motiv.CodeFix/
├── CodeFixProviders/
│   ├── BooleanBlindnessCodeFixProvider.cs (enhanced)
│   ├── CompositionCodeFixProvider.cs
│   ├── CollectionMethodCodeFixProvider.cs
│   └── PolicyPatternCodeFixProvider.cs
├── Converters/
│   ├── ExpressionToSpecConverter.cs (existing)
│   ├── SpecCompositionConverter.cs
│   ├── PolicyGenerator.cs
│   └── HigherOrderSpecConverter.cs
└── Naming/
    └── SemanticNamingService.cs
```

### Dependencies
- **Roslyn SDK:** 4.x (existing)
- **Pattern Matching Library:** Consider Roslyn pattern matching APIs
- **Similarity Detection:** Expression tree comparison algorithms

### Performance Considerations
- Semantic model analysis is expensive
- Cache expression patterns within analysis session
- Limit cross-method analysis scope (same class/namespace)
- Incremental analysis where possible

### Testing Strategy
1. **Unit Tests:** Each analyzer/code fix in isolation
2. **Integration Tests:** End-to-end scenario testing
3. **Performance Tests:** Large codebase analysis benchmarks
4. **Regression Tests:** Existing MOTIV0001 functionality preserved

---

## Documentation Requirements

1. **User Documentation**
   - Rule descriptions for each diagnostic
   - Examples of detected patterns
   - Before/after code samples
   - Configuration options

2. **Developer Documentation**
   - Architecture overview
   - Adding new analyzers guide
   - Pattern detection algorithms
   - Contributing guidelines

3. **Migration Guide**
   - For codebases adopting these analyzers
   - Phased rollout recommendations
   - Handling large numbers of diagnostics

---

## Open Questions

1. **Configuration:**
   - Should users configure similarity threshold for repeated patterns?
   - How to mark operations as "expensive" for short-circuit suggestions?
   - Allow disabling specific diagnostics?

2. **Naming:**
   - Should we provide a customizable pattern library?
   - Domain-specific naming conventions?
   - Integration with domain dictionaries?

3. **Scope:**
   - Limit repeated pattern detection to class? Namespace? Project?
   - Should we analyze test projects differently?
   - What about generated code?

4. **Interactivity:**
   - Multiple code fix options vs. single best suggestion?
   - Interactive naming in IDE?
   - Preview of composed spec before applying?

---

## Future Considerations

### Beyond Phase 4

1. **Learning System:**
   - Learn from user's naming choices
   - Build project-specific pattern library
   - Suggest patterns based on existing specs

2. **Bulk Refactoring:**
   - Apply fixes across entire solution
   - Generate migration report
   - Handle cross-project references

3. **Integration with Motiv Core:**
   - Suggest new Motiv features based on usage patterns
   - Detect opportunities for new composition operators
   - Feedback loop for library evolution

4. **AI-Assisted Naming:**
   - Use ML models for semantic name suggestion
   - Learn from domain context
   - Integrate with code review feedback

---

## References

- **Existing Implementation:**
  - `/home/user/Motiv/src/Motiv.Analyzer/MotivAnalyzer.cs`
  - `/home/user/Motiv/src/Motiv.CodeFix/ConvertToSpecCodeFix.cs`

- **Motiv Core API:**
  - `/home/user/Motiv/Motiv/` (core library)
  - Composition operators: And, AndAlso, Or, OrElse, XOr, Not
  - Collection methods: AndTogether, OrElseTogether, etc.
  - Policy pattern implementation

- **Test Examples:**
  - `/home/user/Motiv/src/Motiv.Analyzer.Tests/`
  - `/home/user/Motiv/src/Motiv.CodeFix.Tests/`
