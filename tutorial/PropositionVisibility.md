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

The `Expression` property will provide a detailed breakdown of the proposition, including encapsulated propositions with
differing metadata types.

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