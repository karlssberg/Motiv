using Motiv.ECommerce.Behaviors;
using Motiv.ECommerce.Models;

namespace Motiv.ECommerce.Policies;

public class ShouldDeliverSameDayPolicy() : Policy<FulfillmentContext, IBehavior>(
    Spec.Build(IsSameDayDelivery)
        .WhenTrue(new SameDayDeliveryBehavior() as IBehavior)
        .WhenFalse( new DefaultBehavior())
        .Create("should deliver same day"))
{
    private static SpecBase<FulfillmentContext, string> IsSameDayDelivery { get; } =
        Spec.From((FulfillmentContext context) => context.DistanceFromStore < 10)
            .Create("is same day delivery");
}
