using Motiv.ECommerce.Behaviors;
using Motiv.ECommerce.Models;
using Motiv.ECommerce.Policies;

namespace Motiv.ECommerce;

public class IsSpecialFulfillmentBehavior()
    : Spec<FulfillmentContext, IBehavior>(
        new ShouldFulfillLocallyPolicy()
        | new ShouldSplitOrderPolicy()
        | new ShouldShipSomeProductsFromStorePolicy()
        | new ShouldDeliverSameDayPolicy());
