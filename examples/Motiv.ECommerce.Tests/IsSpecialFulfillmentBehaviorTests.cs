using FluentAssertions;
using Motiv.ECommerce.Models;

namespace Motiv.ECommerce.Tests;

public class IsSpecialFulfillmentBehaviorTests
{
    [Theory, AutoData]
    public void Should_ship_from_store(
        ShouldShipSomeProductsFromStorePolicy sut,
        FulfillmentContext context)
    {
        var deliveryContext = ToShipFromStoreContext(context);

        var result = sut.IsSatisfiedBy(deliveryContext);

        var act = result.Justification;

        act.Should().BeEquivalentTo(
            """
            should ship from store
                should ship some products from store
                    AND
                        Brand Model is out of stock at warehouse
                        Brand Model is available in store
            """);
    }

    [Theory, AutoData]
    public void Should_evaluate_same_day_delivery(
        ShouldDeliverSameDayPolicy sut,
        FulfillmentContext context)
    {
        var deliveryContext = ToSameDayDeliveryContext(context);

        var result = sut.IsSatisfiedBy(deliveryContext);

        var act = result.Justification;

        act.Should().BeEquivalentTo(
            """
            should deliver same day
                is same day delivery
            """);
    }

    [Theory, AutoData]
    public void Should_evaluate_split_order_delivery(
        ShouldSplitOrderPolicy sut,
        FulfillmentContext context)
    {
        var deliveryContext = ToSplitOrderContext(context);

        var result = sut.IsSatisfiedBy(deliveryContext);

        var act = result.Justification;

        act.Should().BeEquivalentTo(
            """
            should split order
                is expensive
            """);
    }

    [Theory, AutoData]
    public void Should_evaluate_local_fulfillment(
        ShouldFulfillLocallyPolicy sut,
        FulfillmentContext context)
    {
        var deliveryContext = ToLocalDeliveryContext(context);

        var result = sut.IsSatisfiedBy(deliveryContext);

        var act = result.Justification;

        act.Should().BeEquivalentTo(
            """
            should locally fulfill
                any perishable
                    Brand Model is perishable
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

    private static FulfillmentContext ToSameDayDeliveryContext(FulfillmentContext context) =>
        context with
        {
            DistanceFromStore = 1
        };
}
