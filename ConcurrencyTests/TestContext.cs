using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace ConcurrencyTests;

public class TestContext : IAsyncDisposable
{
    private readonly ServiceProvider provider;

    public TestContext()
    {
        var contextId = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddDbContext<ProductDbContext>(o => 
            o.UseInMemoryDatabase(contextId.ToString()));
        this.provider = services.BuildServiceProvider();
        
        var context = this.GetRequiredService<ProductDbContext>();
        context.Database.EnsureCreated();
    }

    public async Task SeedAsync(SimpleProduct product)
    {
        var context = this.GetRequiredService<ProductDbContext>();
        context.Set<SimpleProduct>().Add(product);
        await context.SaveChangesAsync();
    }
    
    public async Task SeedAsync(ConcurrentProduct product)
    {
        var context = this.GetRequiredService<ProductDbContext>();
        context.Set<ConcurrentProduct>().Add(product);
        await context.SaveChangesAsync();
    }

    public T GetRequiredService<T>() where T : notnull
    {
        var scope = this.provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }


    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();
    }
}