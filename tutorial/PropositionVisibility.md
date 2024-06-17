# Proposition Visibility

### Statement

The `Statement` property will provide a simplified high-level representation of the proposition, while the 
`Expression` will go into more detail about the structure.

```csharp
var specA = Spec.Build((bool b) => b).Create("a");
var specB = Spec.Build((bool b) => !b).Create("b");
var specC = Spec.Build((bool b) => !b).Create("c");

var spec = specA & !(specB | specC);

spec.Statement; // "a & !(b | c)"
```

### Expression

Sometimes, this is not enough, and you need to understand the proposition in more detail.
When building complicated expressions made up of many layers of encapsulated propositions, it can be challenging
to understand the overall expression.
While this problem exists regardless of how you decompose your logic, Motiv tries to mitigate this by allowing you to 
inspect (at runtime) the proposition before it is used to evaluate models.

```csharp
var isRoyalFlush =
    Spec.Build(new IsHandFlushProposition() & new IsAceHighStraightBroadwayProposition())
        .WhenTrue(HandRank.RoyalFlush)
        .WhenFalse(HandRank.Unknown)
        .Create("is a royal flush hand")

isRoyalFlush.Expression; // is a royal flush hand
                         //     AND
                         //         is a flush hand
                         //             OR
                         //                 has 5 Clubs cards
                         //                     is Clubs
                         //                 has 5 Diamonds cards
                         //                     is Diamonds
                         //                 has 5 Hearts cards
                         //                     is Hearts
                         //                 has 5 Spades cards
                         //                     is Spades
                         //         is Ace High Straight Broadway
                         //             all cards are Ace, King, Queen, Jack, and Ten

```

`Assertions` will give you a 
friendly list of reasons, but sometimes you need more context.


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