namespace Motiv.ECommerce.Models;

public record FulfillmentContext(Order Order, DateTime OrderDate, double DistanceFromStore);
