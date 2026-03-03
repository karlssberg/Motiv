using AutoFixture;
using Motiv.ECommerce.Models;
using Motiv.ECommerce.Policies;
using Shouldly;

namespace Motiv.ECommerce.Tests;

public class IsSpecialFulfillmentBehaviorTests
{
    [Theory, MemberData(nameof(AllContexts))]
    public void Should_perform_a_special_fulfillment(
        FulfillmentContext deliveryContext)
    {
        // Arrange
        var sut = new IsSpecialFulfillmentBehavior();

        var result = sut.IsSatisfiedBy(deliveryContext);

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBeTrue();
    }

    [Theory, AutoData]
    public void Should_ship_from_store(
        ShouldShipSomeProductsFromStorePolicy sut,
        FulfillmentContext context)
    {
        // Arrange
        var deliveryContext = ToShipFromStoreContext(context);

        var result = sut.IsSatisfiedBy(deliveryContext);

        // Act
        var act = result.Justification;

        // Assert
        act.ShouldBe(
            """
            should ship from store == true
                should ship some products from store == true
                    AND (1)
                        is out of stock == true
                            (InventoryPricedProduct product) => product.WarehouseStockLevel == 0 == true
                                product.WarehouseStockLevel == 0
                        is available in store == true
                            (InventoryPricedProduct product) => product.InStoreStockLevel > 0 == true
                                product.InStoreStockLevel > 0
            """);
    }

    [Theory, AutoData]
    public void Should_evaluate_same_day_delivery(
        ShouldDeliverSameDayPolicy sut,
        FulfillmentContext context)
    {
        // Arrange
        var deliveryContext = ToSameDayDeliveryContext(context);

        var result = sut.IsSatisfiedBy(deliveryContext);

        // Act
        var act = result.Justification;

        // Assert
        act.ShouldBe(
            """
            should deliver same day == true
                is same day delivery == true
                    (FulfillmentContext context) => context.DistanceFromStore < 10 == true
                        context.DistanceFromStore < 10
            """);
    }

    [Theory, AutoData]
    public void Should_evaluate_split_order_delivery(
        ShouldSplitOrderPolicy sut,
        FulfillmentContext context)
    {
        // Arrange
        var deliveryContext = ToSplitOrderContext(context);

        var result = sut.IsSatisfiedBy(deliveryContext);

        // Act
        var act = result.Justification;

        // Assert
        act.ShouldBe(
            """
            should split order == true
                is expensive == true
                    (FulfillmentContext context) => context.Order.Products.Sum((InventoryPricedProduct product) => product.Price) > 1000 == true
                        context.Order.Products.Sum((InventoryPricedProduct product) => product.Price) > 1000
            """);
    }

    [Theory, AutoData]
    public void Should_evaluate_local_fulfillment(
        ShouldFulfillLocallyPolicy sut,
        FulfillmentContext context)
    {
        // Arrange
        var deliveryContext = ToLocalDeliveryContext(context);

        var result = sut.IsSatisfiedBy(deliveryContext);

        // Act
        var act = result.Justification;

        // Assert
        act.ShouldBe(
            """
            should locally fulfill == true
                any perishable == true
                    (InventoryPricedProduct product) => product.ExpireDate - product.DateInStock < TimeSpan.FromDays(30) == true (1)
                        product.ExpireDate - product.DateInStock < TimeSpan.FromDays(30)
            """);
    }

    private static FulfillmentContext ToLocalDeliveryContext(FulfillmentContext context) =>
        context with
        {
            Order = context.Order with
            {
                Products =
                [
                    new InventoryPricedProduct("Brand", "Model")
                    {
                        WarehouseStockLevel = 0,
                        InStoreStockLevel = 1,
                        DateInStock = context.OrderDate.AddDays(-1),
                        ExpireDate = context.OrderDate.AddDays(1),
                        Price = 1001
                    }
                ]
            }
        };

    private static FulfillmentContext ToSplitOrderContext(FulfillmentContext context) =>
        context with
        {
            Order = context.Order with
            {
                Products =
                [
                    new InventoryPricedProduct("Brand", "Model")
                    {
                        WarehouseStockLevel = 0,
                        InStoreStockLevel = 1,
                        DateInStock = default,
                        Price = 1001
                    }
                ]
            }
        };

    private static FulfillmentContext ToShipFromStoreContext(FulfillmentContext context) =>
        context with
        {
            Order = context.Order with
            {
                Products =
                [
                    new InventoryPricedProduct("Brand", "Model")
                    {
                        WarehouseStockLevel = 0,
                        InStoreStockLevel = 1,
                        DateInStock = default,
                        Price = 100
                    }
                ]
            }
        };

    public static IEnumerable<object[]> AllContexts()
    {
        var fixture = new Fixture();
        yield return [ToLocalDeliveryContext(fixture.Create<FulfillmentContext>())];
        yield return [ToSplitOrderContext(fixture.Create<FulfillmentContext>())];
        yield return [ToShipFromStoreContext(fixture.Create<FulfillmentContext>())];
        yield return [ToSameDayDeliveryContext(fixture.Create<FulfillmentContext>())];
    }

    private static FulfillmentContext ToSameDayDeliveryContext(FulfillmentContext context) =>
        context with
        {
            DistanceFromStore = 1
        };
}
