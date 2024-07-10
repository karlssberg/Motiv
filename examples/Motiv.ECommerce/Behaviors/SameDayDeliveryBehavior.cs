using Motiv.ECommerce.Models;

namespace Motiv.ECommerce.Behaviors;

public class SameDayDeliveryBehavior : IBehavior
{
    public Task PrepareShipment(ShippingDetails details) => throw new NotImplementedException();
}