using Motiv.ECommerce.Models;

namespace Motiv.ECommerce.Behaviors;

public interface IBehavior
{
    Task PrepareShipment(ShippingDetails details);
}
