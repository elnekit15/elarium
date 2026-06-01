using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using DesignerStore.Models;
using Microsoft.EntityFrameworkCore;

namespace DesignerStore.Data;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Product>          Products          { get; set; }
    public DbSet<ProductImage>     ProductImages      { get; set; }
    public DbSet<ProductSizeStock> ProductSizeStocks  { get; set; }
    public DbSet<Order>            Orders             { get; set; }
    public DbSet<OrderItem>        OrderItems         { get; set; }
}