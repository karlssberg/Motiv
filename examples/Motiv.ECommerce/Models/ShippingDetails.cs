namespace Motiv.ECommerce.Models;

public record ShippingDetails(IEnumerable<InventoryPricedProduct> Products, Order order);
