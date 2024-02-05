using Microsoft.EntityFrameworkCore;

namespace ConcurrencyTests;

public class ProductDbContext : DbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SimpleProduct>().ToTable(nameof(SimpleProduct));
        modelBuilder.Entity<SimpleProduct>().HasKey(o => o.Id);
        modelBuilder.Entity<SimpleProduct>().Property(o => o.Id).ValueGeneratedOnAdd().IsRequired();
        modelBuilder.Entity<SimpleProduct>().Property(o => o.Title).IsRequired();
        modelBuilder.Entity<SimpleProduct>().Property(o => o.Price).IsRequired();
        
        modelBuilder.Entity<ConcurrentProduct>().ToTable(nameof(ConcurrentProduct));
        modelBuilder.Entity<ConcurrentProduct>().HasKey(o => o.Id);
        modelBuilder.Entity<ConcurrentProduct>().Property(o => o.Id).ValueGeneratedOnAdd().IsRequired();
        modelBuilder.Entity<ConcurrentProduct>().Property(o => o.Title).IsRequired();
        modelBuilder.Entity<ConcurrentProduct>().Property(o => o.Price).IsRequired();
        modelBuilder.Entity<ConcurrentProduct>().Property(o => o.VersionNumber).IsConcurrencyToken().IsRequired();
        
        base.OnModelCreating(modelBuilder);
    }
}