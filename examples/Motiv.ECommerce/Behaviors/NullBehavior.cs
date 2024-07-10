using Motiv.ECommerce.Models;

namespace Motiv.ECommerce.Behaviors;

public class NullBehavior : IBehavior
{
    public Task PrepareShipment(ShippingDetails details) => throw new NotImplementedException();
}