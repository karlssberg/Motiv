using Motiv.ECommerce.Models;

namespace Motiv.ECommerce.Behaviors;

public class SplitOrderBehavior : IBehavior
{
    public Task PrepareShipment(ShippingDetails details) => throw new NotImplementedException();
}