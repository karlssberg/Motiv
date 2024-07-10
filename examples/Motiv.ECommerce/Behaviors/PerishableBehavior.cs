using Motiv.ECommerce.Models;

namespace Motiv.ECommerce.Behaviors;

public class PerishableBehavior : IBehavior
{
    public Task PrepareShipment(ShippingDetails details) => throw new NotImplementedException();
}
