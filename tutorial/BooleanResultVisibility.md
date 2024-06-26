# Boolean Result Visibility

While `Assertions` will give you a friendly list of reasons, sometimes you need more context.

The `Reason` property will provide a simplified high-level explanation of what happened, while the `Justification`
will go into more detail about the causes.  Both show the structure of the propositions that influenced the result.

### Reason
```csharp
var specA = Spec.Build((bool b) => b).Create("a");
var specB = Spec.Build((bool b) => !b).Create("b");
var specC = Spec.Build((bool b) => !b).Create("c");

var spec = specA & !(specB | specC);

var act = spec.IsSatisfiedBy(true);

act.Reason; // "a & !(¬b | ¬c)"
```

The operators used in the `Reason` property are there to indicate how the propositions are combined.
You will notice that the `¬` symbol is used to indicate that a proposition resolved to false, and is not used
indicate that the original sub-expression was negated, which instead uses the `!` symbol.
Presenting the expression in this way allows you to quickly identify a negative-assertion from a NOT operator.

### Justification

While `Reason` property will provide a simplified
explanation of what happened, the `Justification` will give you a thorough breakdown of the causes, including
encapsulated propositions with differing metadata types.
Any propositions that did not influence the final result are omitted.

Note that the `Justification` property uses the prefix/polish notation
(to balance readability with computational efficiency).

    <statement/operator>
        <first>
        <second>
        <third>

Below is an example of identifying a _straight flush_ hand in a poker game.

```csharp
var sut = new IsWinningHandProposition();

var act = sut.IsSatisfiedBy(new Hand("KH, QH, JH, 10H, 9H");

act.Justification; // is a winning poker hand
                   //     OR
                   //         is a straight flush hand
                   //             AND
                   //                 is a straight hand
                   //                     OR
                   //                         is King High Straight
                   //                             all cards are King, Queen, Jack
                   //                 is a flush hand
                   //                     OR
                   //                         a flush of Hearts
                   //                             KH is Hearts
                   //                             QH is Hearts
                   //                             JH is Hearts
```

Notice how the `Justification` property only shows the assertions that influenced the outcome, and that it withholds
any assertions that may have been calculated but did not influence the final result.
