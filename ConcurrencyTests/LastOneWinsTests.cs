using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace ConcurrencyTests;

public class LastOneWinsTests
{
    private readonly ITestOutputHelper output;
    private readonly ManualResetEvent synchronizationEvent = new(false);
    private readonly ManualResetEvent[] taskEvents = [new ManualResetEvent(false), new ManualResetEvent(false)];
    private readonly SimpleProduct initialProduct = new() {Id = 1, Price = 0.99m, Title = "Cucumber"};

    public LastOneWinsTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task ReleasingFirstTask_LeadsToLastTasksValues()
    {
        var context = new TestContext();
        await context.SeedAsync(initialProduct);

        Action<SimpleProduct> updateAction0 = p => { p.Title = "Cucumber-0"; };
        Action<SimpleProduct> updateAction1 = p => { p.Title = "Cucumber-1"; };

        var task0 = Task.Run(async () => await UpdateProductAsync(context, 0, updateAction0));
        var task1 = Task.Run(async () => await UpdateProductAsync(context, 1, updateAction1));

        synchronizationEvent.Set();
        await Task.Delay(TimeSpan.FromMilliseconds(10));

        taskEvents[0].Set();
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        taskEvents[1].Set();

        await task0;
        await task1;

        var product = await GetAndPrintProductProperties(context);
        product.Title.Should().Be("Cucumber-1");
        
        await context.DisposeAsync();
    }
    
    [Fact]
    public async Task ReleasingLastTask_LeadsToFirstTasksValues()
    {
        var context = new TestContext();
        await context.SeedAsync(initialProduct);

        Action<SimpleProduct> updateAction0 = p => { p.Title = "Cucumber-0"; };
        Action<SimpleProduct> updateAction1 = p => { p.Title = "Cucumber-1"; };

        var task0 = Task.Run(async () => await UpdateProductAsync(context, 0, updateAction0));
        var task1 = Task.Run(async () => await UpdateProductAsync(context, 1, updateAction1));

        synchronizationEvent.Set();
        await Task.Delay(TimeSpan.FromMilliseconds(10));

        taskEvents[1].Set();
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        taskEvents[0].Set();

        await task0;
        await task1;

        var product = await GetAndPrintProductProperties(context);
        product.Title.Should().Be("Cucumber-0");
        
        await context.DisposeAsync();
    }
    
    [Fact]
    public async Task UpdatingMultipleProperties_WithEfCoreOptimization_LeadsToCorruptValues()
    {
        var context = new TestContext();
        await context.SeedAsync(initialProduct);

        Action<SimpleProduct> updateAction0 = p =>
        {
            p.Title = "Cucumber";
            p.Price = 1.99m;
        };
        Action<SimpleProduct> updateAction1 = p =>
        {
            p.Title = "Cucumber-1";
            p.Price = 0.99m;
        };

        var task0 = Task.Run(async () => await UpdateProductAsync(context, 0, updateAction0));
        var task1 = Task.Run(async () => await UpdateProductAsync(context, 1, updateAction1));

        synchronizationEvent.Set();
        await Task.Delay(TimeSpan.FromMilliseconds(10));

        taskEvents[0].Set();
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        taskEvents[1].Set();

        await task0;
        await task1;

        var product = await GetAndPrintProductProperties(context);
        product.Title.Should().Be("Cucumber-1");
        product.Price.Should().Be(0.99m);
        
        await context.DisposeAsync();
    }

    private async Task<SimpleProduct> GetAndPrintProductProperties(TestContext context)
    {
        var dbContext = context.GetRequiredService<ProductDbContext>();
        var existingProduct = await dbContext.Set<SimpleProduct>().FindAsync(initialProduct.Id);
        output.WriteLine($"Title: {existingProduct!.Title}");
        output.WriteLine($"Price: {existingProduct.Price}");
        return existingProduct;
    }

    private async Task UpdateProductAsync(TestContext context, int taskId, Action<SimpleProduct> updateAction)
    {
        var dbContext = context.GetRequiredService<ProductDbContext>();
        var existingProduct = await dbContext.Set<SimpleProduct>().FindAsync(initialProduct.Id);

        output.WriteLine($"Task {taskId} retrieved product");
        synchronizationEvent.WaitOne();
        output.WriteLine($"Task {taskId} performing update");
        updateAction(existingProduct!);
        taskEvents[taskId].WaitOne();
        output.WriteLine($"Task {taskId} saving");
        await dbContext.SaveChangesAsync();
    }
}