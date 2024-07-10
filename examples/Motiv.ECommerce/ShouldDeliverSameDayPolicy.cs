using Motiv.ECommerce.Behaviors;
using Motiv.ECommerce.Models;

namespace Motiv.ECommerce;

public class ShouldDeliverSameDayPolicy() : Policy<FulfillmentContext, IBehavior>(
    Spec.Build(IsSameDayDelivery)
        .WhenTrue(new SameDayDeliveryBehavior() as IBehavior)
        .WhenFalse( new NullBehavior())
        .Create("should deliver same day"))
{
    private static SpecBase<FulfillmentContext, string> IsSameDayDelivery { get; } =
        Spec.Build((FulfillmentContext context) => context.DistanceFromStore < 10)
            .Create("is same day delivery");
}
