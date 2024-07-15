using Motiv.ECommerce;
using Motiv.ECommerce.Models;

var competitorsProducts = new Dictionary<ProductId,PricedProduct>
{
    [new ProductId("Apple", "iPhone 12")]    = new("Apple", "iPhone 12") { Price = 799.99m },
    [new ProductId("Samsung", "Galaxy S21")] = new("Samsung", "Galaxy S21") { Price = 699.99m },
    [new ProductId("Google", "Pixel 5")]     = new("Google", "Pixel 5") { Price = 699.99m },
};

var stock = new HashSet<InventoryPricedProduct>
{
    new("Apple", "iPhone 12")
    {
        InStoreStockLevel = 5,
        WarehouseStockLevel = 10,
        DateInStock = DateTime.Now.AddDays(-100),
        Price = 899.99m
    },
    new("Samsung", "Galaxy S21")
    {
        InStoreStockLevel = 2,
        WarehouseStockLevel = 5,
        DateInStock = DateTime.Now.AddDays(-90),
        Price = 649.99m
    },
    new("Google", "Pixel 5")
    {
        InStoreStockLevel = 10,
        WarehouseStockLevel = 0,
        DateInStock = DateTime.Now.AddDays(-80),
        Price = 699.99m
    }
};

var context = new DynamicPricingPolicy.Context(
    stock.First(),
    new User("John", new HashSet<Product> { stock.First() }),
    DateTime.Now);

var result = new DynamicPricingPolicy(competitorsProducts).IsSatisfiedBy(context);

Console.WriteLine($"The new price of {context.Product.Brand} {context.Product.Model} is {result.Value} (originally {context.Product.Price})");

Console.WriteLine(result.Justification);
