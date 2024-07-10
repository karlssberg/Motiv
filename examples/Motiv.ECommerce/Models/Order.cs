namespace Motiv.ECommerce.Models;

public record Order(IEnumerable<InventoryPricedProduct> Products, User User);
