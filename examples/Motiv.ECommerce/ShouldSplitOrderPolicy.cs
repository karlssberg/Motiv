using Motiv.ECommerce.Behaviors;
using Motiv.ECommerce.Models;

namespace Motiv.ECommerce;

public class ShouldSplitOrderPolicy() : Policy<FulfillmentContext, IBehavior>(
    Spec.Build(IsExpensive)
        .WhenTrue(new SplitOrderBehavior() as IBehavior)
        .WhenFalse(new NullBehavior())
        .Create("should split order"))
{
    private static SpecBase<FulfillmentContext, string> IsExpensive { get; } =
        Spec.Build((FulfillmentContext context) => context.Order.Products.Sum(product => product.Price) > 1000)
            .Create("is expensive");
}
