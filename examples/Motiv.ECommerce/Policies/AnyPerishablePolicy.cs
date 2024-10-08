using Motiv.ECommerce.Behaviors;
using Motiv.ECommerce.Models;

namespace Motiv.ECommerce.Policies;

public class ShouldFulfillLocallyPolicy() : Policy<FulfillmentContext, IBehavior>(
    Spec.Build(AnyPerishable)
        .WhenTrue(new PerishableBehavior() as IBehavior)
        .WhenFalse(new DefaultBehavior())
        .Create("should locally fulfill"))
{
    private static SpecBase<FulfillmentContext, string> AnyPerishable { get; } =
        Spec.From((InventoryPricedProduct product) => product.ExpireDate - product.DateInStock < TimeSpan.FromDays(30))
            .AsAnySatisfied()
            .WhenTrueYield(eval => eval.CausalModels.Select(p =>  $"{p.Brand} {p.Model} is perishable"))
            .WhenFalse("has no perishable products")
            .Create("any perishable")
            .ChangeModelTo<FulfillmentContext>(ctx => ctx.Order.Products);
}
