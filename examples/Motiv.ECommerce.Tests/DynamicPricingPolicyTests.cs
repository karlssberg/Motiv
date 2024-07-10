using FluentAssertions;
using Motiv.ECommerce.Models;

namespace Motiv.ECommerce.Tests;

public class DynamicPricingPolicyTests
{
    private static readonly DateTime Now = DateTime.Parse("2022-01-01T12:00:00Z");
    private static Dictionary<ProductId,PricedProduct> CompetitorsProducts { get; } = new()
    {
        [new ProductId("Apple", "iPhone 12")]    = new PricedProduct("Apple", "iPhone 12")
                                                            {
                                                                Price = 799.99m
                                                            },
        [new ProductId("Samsung", "Galaxy S21")] = new PricedProduct("Samsung", "Galaxy S21")
                                                            {
                                                                Price = 699.99m
                                                            },
        [new ProductId("Google", "Pixel 5")]     = new PricedProduct("Google", "Pixel 5")
                                                            {
                                                                Price = 699.99m
                                                            },
    };

    private static HashSet<InventoryPricedProduct> Stock =
    [
        new InventoryPricedProduct("Apple", "iPhone 12")
        {
            Price = 899.99m,
            InStoreStockLevel = 5,
            WarehouseStockLevel = 10,
            DateInStock = Now.AddDays(-100)
        },
        new InventoryPricedProduct("Samsung", "Galaxy S21")
        {
            Price = 649.99m,
            InStoreStockLevel = 15,
            WarehouseStockLevel = 5,
            DateInStock = Now.AddDays(-90)

        },
        new InventoryPricedProduct("Google", "Pixel 5")
        {
            Price = 699.99m,
            InStoreStockLevel = 0,
            WarehouseStockLevel = 1,
            DateInStock = Now.AddDays(-80)
        }
    ];

    public static IEnumerable<object[]> Data =>
        new List<object[]>
        {
            new object[]
            {
                new DynamicPricingPolicy.Context(
                    Stock.ElementAt(0),
                    new User("John",
                        [Stock.ElementAt(0)]),
                    DateTime.Parse("2022-01-01T12:00:00Z")),
                799.99m
            },
            new object[]
            {
                new DynamicPricingPolicy.Context(
                    Stock.ElementAt(1),
                    new User("John", [Stock.ElementAt(1)]),
                    DateTime.Parse("2022-01-01T03:00:00Z")),
                649.99m * (1m - 0.1m - 0.02m - (-0.2m))
            },
            new object[]
            {
                new DynamicPricingPolicy.Context(
                    Stock.ElementAt(2),
                    new User("John", [Stock.ElementAt(2)]),
                    DateTime.Parse("2022-01-01T12:00:00Z")),
                699.99m * (1m - 0.02m - (-0.2m))
            }
        };

    [Theory]
    [MemberData(nameof(Data))]
    public void Should_evaluate_discount(DynamicPricingPolicy.Context context, decimal expected)
    {
        // Arrange
        var sut = new DynamicPricingPolicy(CompetitorsProducts);

        var result = sut.Execute(context);

        // Act
        var act = result.Value;

        // Assert
        act.Should().Be(expected, result.RootAssertions.Serialize());
    }
}
