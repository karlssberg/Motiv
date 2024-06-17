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

var sut = spec.IsSatisfiedBy(true);
   
act.Reason; // "a & (!b | !c)"
```

The operators used in the `Reason` property are there to indicate how the propositions are combined.
You will notice that the `!` symbol is only used to indicate that a proposition resolved to false, and is not used
indicate that the original sub-expression was negated.
Presenting the expression in this way allows you to quickly understand the structure of underlying causes instead of
the structure of the original proposition.

### Justification

While `Reason` property will provide a simplified 
explanation of what happened, the `Justification` will give you a thorough breakdown of the causes, including 
encapsulated propositions with differing metadata types.
Any propositions that did not influence the final result are omitted.

```csharp
var sut = new IsWinningHandProposition();

var act = sut.IsSatisfiedBy(new Hand("KH, QH, JH, 10H, 9H");

act.Justification; // is a winning poker hand
                   //     OR
                   //         is a royal flush hand
                   //             AND
                   //                 is a flush hand
                   //                     OR
                   //                         a flush of Hearts
                   //                             AH is Hearts
                   //                             KH is Hearts
                   //                             QH is Hearts
                   //                             JH is Hearts
                   //                             10H is Hearts
                   //                 is Ace High Straight Broadway
                   //                     all cards are Ace, King, Queen, Jack, and Ten
```