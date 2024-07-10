namespace Motiv.ECommerce.Models;

public record PricedProduct(string Brand, string Model) : Product(Brand, Model)
{
    public required decimal Price { get; init; }
}