# Detailed Breakdown

When building complicated expressions, it can be challenging to comprehend the causes.  `Assertions` will give you a 
friendly list of reasons, but sometimes you need more context.

While `Reason` property will provide a simplified 
explanation of what happened, the `Justification` will give you a thorough breakdown of the causes, including 
encapsulated propositions with differing metadata types.

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
                   //                             all cards are King, Queen, Jack, Ten, and Nine
                   //                 is a flush hand
                   //                     OR
                   //                         a flush of Hearts
                   //                             KH is Hearts
                   //                             QH is Hearts
                   //                             JH is Hearts
                   //                             10H is Hearts
                   //                             9H is Hearts
```