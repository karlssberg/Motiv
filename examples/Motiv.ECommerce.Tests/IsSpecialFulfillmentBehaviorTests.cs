using AutoFixture;
using FluentAssertions;
using Motiv.ECommerce.Models;
using Motiv.ECommerce.Policies;

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
        act.Should().BeTrue();
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
        act.Should().BeEquivalentTo(
            """
            should ship from store
                should ship some products from store
                    AND
                        Brand Model is out of stock at warehouse
                            product.WarehouseStockLevel == 0
                        Brand Model is available in store
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
        act.Should().BeEquivalentTo(
            """
            should deliver same day
                is same day delivery
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
        act.Should().BeEquivalentTo(
            """
            should split order
                is expensive
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
        act.Should().BeEquivalentTo(
            """
            should locally fulfill
                any perishable
                    Brand Model is perishable
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

    private static FulfillmentContext ToContext(FulfillmentContext context) =>
        context with
        {
            Order = context.Order with
            {
                Products = context.Order.Products
                    .Select(product => product with
                    {
                        ExpireDate = context.OrderDate.AddDays(1),
                        DateInStock = context.OrderDate.AddDays(-1)
                    })
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
