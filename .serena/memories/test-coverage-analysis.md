# Test Coverage Analysis for ResultDescriptionBase and Binary Operators

## Test Project Structure

**Main Test Project**: `/home/dan/repos/Motiv/src/Motiv.Tests/Motiv.Tests.csproj`
- Multi-target: net8.0, net9.0, net472, net10.0
- Uses xUnit with AutoFixture and Shouldly for assertions
- Has a Customizations folder with `ResultDescriptionBaseCustomization.cs`

## Key Test Files and Coverage

### Primary Description Tests

1. **PropositionResultDescriptionTests.cs** (1062 lines)
   - Most comprehensive description tests
   - Tests Reason, Justification, GetJustificationAsLines
   - Tests simple propositions, composition, higher-order specs
   - Tests operator collapsing (AND/ANDALSO, OR/ORELSE)
   - XOR doesn't collapse
   - Tests binary operation negation with various patterns

2. **ExpressionTreeJustificationTests.cs** (150+ lines)
   - Tests justification output for expression trees
   - Tests both minimal propositions and with WhenTrue/WhenFalse
   - Tests hierarchical breakdown of expression trees
   - Tests higher-order propositions with expression trees

### Binary Operator Specific Tests

3. **AndSpecTests.cs**
   - Tests `&` operator
   - Tests logical AND behavior
   - Tests reason serialization (de-noised: shows only causal operands)
   - Pattern: `(left == true) & (right == true)` when both true
   - Pattern: `right == false` when only right is false (no parens for single operand)

4. **OrSpecTests.cs**
   - Tests `|` operator
   - Tests logical OR behavior
   - Tests reason serialization
   - Similar de-noising pattern

5. **XOrSpecTests.cs**
   - Tests `^` operator
   - XOR always shows both operands: `(left == true) ^ (right == true)`
   - XOR never collapses in descriptions

6. **AndAlsoSpecTests.cs**
   - Tests `.AndAlso()` method (short-circuit AND)
   - Tests short-circuit behavior (right not evaluated if left false)
   - Reason format: `"not left"` or `"left && right"`
   - Uses `&&` operator in reason instead of `&`

7. **OrElseSpecTests.cs**
   - Tests `.OrElse()` method (short-circuit OR)
   - Tests short-circuit behavior (right not evaluated if left true)
   - Reason format: `"not left || not right"` or `"left"` or `"right"`
   - Uses `||` operator in reason instead of `|`

8. **NotSpecTests.cs**
   - Tests `!` operator
   - Tests negation of propositions
   - Reason format: `"!(is true == true)"` or `"!True"` (for string metadata)

### Policy-Specific Tests

9. **NotPolicyTests.cs** - NOT on policies
10. **OrElsePolicyTests.cs** - short-circuit OR on policies

## Test Customizations

**ResultDescriptionBaseCustomization.cs**: Test double implementation
```csharp
public class ResultDescription(string statement, string reason, IEnumerable<string> justification) 
    : ResultDescriptionBase
{
    internal override string Statement => statement;
    public override string Reason => reason;
    internal override int CausalOperandCount => 1;
    public override IEnumerable<string> GetJustificationAsLines() => justification;
}
```

## Key Test Patterns & Coverage Areas

### Reason Formatting Rules (De-noising)
1. **Single causal operand**: no parentheses
2. **Multiple causal operands**: each wrapped in parens with operator between
3. **XOR**: always shows both operands with parens
4. **Short-circuit operators**: use `&&`/`||` instead of `&`/`|`
5. **Equality assertions**: proposed name + `== true/false` suffix when metadata is non-string

### Justification Output
1. Hierarchical tree structure with indentation
2. Operator names in uppercase (AND, OR, XOR, NAND, NOR)
3. De-noised (only causal assertions shown)
4. Custom assertions included in tree

### Operator Collapsing in Descriptions
1. AND and ANDALSO can collapse together
2. OR and ORELSE can collapse together
3. XOR never collapses
4. Prevents duplicate operator syntax

## How to Run Tests

```bash
# From /home/dan/repos/Motiv
dotnet test src/Motiv.Tests/Motiv.Tests.csproj

# Run specific test file
dotnet test src/Motiv.Tests/Motiv.Tests.csproj --filter PropositionResultDescriptionTests

# Run with coverage
dotnet test src/Motiv.Tests/Motiv.Tests.csproj /p:CollectCoverage=true
```

## Test Data Reference
- Uses InlineData and InlineAutoData (xUnit + AutoFixture)
- Typically 4 scenarios per binary operator (all combinations: TT, TF, FT, FF)
- Extensive parameterized tests with expected output strings
