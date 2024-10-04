using Motiv.ECommerce.Models;

namespace Motiv.ECommerce;

public class DynamicPricingPolicy(Dictionary<ProductId, PricedProduct> competitorsProducts)
    : Policy<DynamicPricingPolicy.Context, decimal>(IsPriceMatch(competitorsProducts).OrElse(IsRegularDiscount))
{
    public record Context(InventoryPricedProduct Product, User User, DateTime Now);

    private static PolicyBase<Context, decimal> IsRegularDiscount =>
        Spec.Build(IsInStockFor90daysDiscount
                   | IsNightOwlTimeDiscount
                   | IsPreviouslyAbandonedProductDiscount
                   | IsLowStockIncrease)
            .WhenTrue((ctx, result) => ctx.Product.Price * (1 - result.Values.Sum()))
            .WhenFalse(ctx => ctx.Product.Price)
            .Create("a discount is applied");

    private static PolicyBase<Context, decimal> IsPriceMatch(Dictionary<ProductId, PricedProduct> competitorsProducts) =>
        Spec.From((Context ctx) => ctx.Product.Price > competitorsProducts[ctx.Product.Id].Price)
            .WhenTrue(ctx => competitorsProducts[ctx.Product.Id].Price)
            .WhenFalse(ctx => ctx.Product.Price)
            .Create("is a competitor's price match");

    private static PolicyBase<Context, decimal> IsInStockFor90daysDiscount { get; } =
        Spec.From((Context ctx) => ctx.Product.DateInStock < ctx.Now.AddDays(-90))
            .WhenTrue(0.05m)
            .WhenFalse(0m)
            .Create("the product has been in stock for 90 days");

    private static PolicyBase<Context, decimal> IsNightOwlTimeDiscount { get; } =
        Spec.Build((Context ctx) => ctx.Now.Hour is >= 1 and < 5)
            .WhenTrue(0.1m)
            .WhenFalse(0m)
            .Create("it is night owl time");

    private static PolicyBase<Context, decimal> IsPreviouslyAbandonedProductDiscount { get; } =
        Spec.From((Context ctx) => ctx.User.AbandonedCartProducts.Contains(ctx.Product))
            .WhenTrue(0.02m)
            .WhenFalse(0m)
            .Create("the product was previously in an abandoned cart");

    private static PolicyBase<Context, decimal> IsLowStockIncrease { get; } =
        Spec.From((Context ctx) => ctx.Product.WarehouseStockLevel < 10)
            .WhenTrue(-0.2m)
            .WhenFalse(0m)
            .Create("there are ten or fewer items in stock");
}
