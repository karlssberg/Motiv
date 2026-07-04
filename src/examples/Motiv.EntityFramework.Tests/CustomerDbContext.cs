using Microsoft.EntityFrameworkCore;

namespace Motiv.EntityFramework.Tests;

public class Customer
{
    public int Id { get; set; }
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

public class CustomerDbContext(DbContextOptions<CustomerDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
}
