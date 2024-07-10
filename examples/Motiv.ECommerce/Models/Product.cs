namespace Motiv.ECommerce.Models;

public record Product(string Brand, string Model)
{
    public DateTime? ExpireDate { get; init; }

    public ProductId Id { get; } = new(Brand, Model);
}

public record ProductId(string Brand, string Model);
