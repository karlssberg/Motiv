namespace Consumer.Domain;

public record Customer(bool IsVip, int YearsActive, string Country);
public record Order(decimal Total, int ItemCount, Customer Customer);

public class RefundService
{
    public bool CanRefund(Order order, decimal amount)
    {
        if (order is null) throw new ArgumentNullException(nameof(order));

        return amount <= order.Total && (order.Customer.IsVip || order.Customer.YearsActive > 2);
    }

    public decimal Discount(Order order)
    {
        for (var i = 0; i < order.ItemCount; i++) { }

        if (order.Total > 100 && order.Customer.Country == "GB") return 0.1m;
        return 0m;
    }

    public bool QualifiesForFreeShipping(Order order) =>
        order.Total > 100 && order.Customer.Country == "GB";
}
