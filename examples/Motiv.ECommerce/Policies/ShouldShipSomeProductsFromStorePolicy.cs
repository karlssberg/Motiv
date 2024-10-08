using Motiv.ECommerce.Behaviors;
using Motiv.ECommerce.Models;

namespace Motiv.ECommerce.Policies;

public class ShouldShipSomeProductsFromStorePolicy() : Policy<FulfillmentContext, IBehavior>(
    Spec.Build(AnyShouldShipFromStore)
        .WhenTrue(new ShipFromStoreBehavior() as IBehavior)
        .WhenFalse(new DefaultBehavior())
        .Create("should ship from store"))
{

    private static SpecBase<FulfillmentContext, string> AnyShouldShipFromStore =>
        Spec.Build(ShouldShipProductFromStore)
            .AsAnySatisfied()
            .Create("should ship some products from store")
            .ChangeModelTo<FulfillmentContext>(ctx => ctx.Order.Products);

    private static SpecBase<InventoryPricedProduct, string> ShouldShipProductFromStore()
    {
        var isOutOfWarehouseStock =
            Spec.From((InventoryPricedProduct product) => product.WarehouseStockLevel == 0)
                .WhenTrue(product => $"{product.Brand} {product.Model} is out of stock at warehouse")
                .WhenFalse(product => $"{product.Brand} {product.Model} is in stock at warehouse")
                .Create("is out of stock");

        var availableInStore =
            Spec.From((InventoryPricedProduct product) => product.InStoreStockLevel > 0)
                .WhenTrue(product => $"{product.Brand} {product.Model} is available in store")
                .WhenFalse(product => $"{product.Brand} {product.Model} is not available in store")
                .Create("is available in store");

        return isOutOfWarehouseStock & availableInStore;
    }
}
