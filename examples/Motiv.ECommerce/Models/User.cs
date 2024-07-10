namespace Motiv.ECommerce.Models;

public record User(string Name, HashSet<Product> AbandonedCartProducts);