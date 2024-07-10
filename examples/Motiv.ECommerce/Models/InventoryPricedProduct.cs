namespace Motiv.ECommerce.Models;

public record InventoryPricedProduct(string Brand, string Model)
    : PricedProduct(Brand, Model)
{
    public required int WarehouseStockLevel { get; init; }

    public required int InStoreStockLevel { get; init; }
    public required DateTime DateInStock { get; init; }
}
