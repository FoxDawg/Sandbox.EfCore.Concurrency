using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace ConcurrencyTests;

public class ConcurrentProductTests
{
    private readonly ITestOutputHelper output;
    private readonly ManualResetEvent synchronizationEvent = new(false);
    private readonly ManualResetEvent[] taskEvents = [new ManualResetEvent(false), new ManualResetEvent(false)];
    private readonly ConcurrentProduct initialProduct = new() {Id = 1, Price = 0.99m, Title = "Cucumber", VersionNumber = 1};

    public ConcurrentProductTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task ReleasingFirstTask_LeadsToLastTasksValues()
    {
        var context = new TestContext();
        await context.SeedAsync(initialProduct);

        Action<ConcurrentProduct, ProductDbContext> updateAction0 = (p, _) => { 
            p.Title = "Cucumber-0";
            ++p.VersionNumber;
        };
        Action<ConcurrentProduct, ProductDbContext> updateAction1 = (p, _) =>
        {
            p.Title = "Cucumber-1";
            ++p.VersionNumber;
        };

        var task0 = Task.Run(async () => await UpdateProductAsync(context, 0, updateAction0));
        var task1 = Task.Run(async () => await UpdateProductAsync(context, 1, updateAction1));

        synchronizationEvent.Set();
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        taskEvents[0].Set();
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        taskEvents[1].Set();

        await task0;
        await task1;

        var product = await GetAndPrintProductProperties(context);
        product.Title.Should().Be("Cucumber-1");
        product.VersionNumber.Should().Be(3);
        
        await context.DisposeAsync();
    }
    
    [Fact]
    public async Task ReleasingLastTask_LeadsToFirstTasksValues()
    {
        var context = new TestContext();
        await context.SeedAsync(initialProduct);

        Action<ConcurrentProduct, ProductDbContext> updateAction0 = (p, _) =>
        {
            p.Title = "Cucumber-0";
            ++p.VersionNumber;
        };
        Action<ConcurrentProduct, ProductDbContext> updateAction1 = (p, _) =>
        {
            p.Title = "Cucumber-1";
            ++p.VersionNumber;
        };

        var task0 = Task.Run(async () => await UpdateProductAsync(context, 0, updateAction0));
        var task1 = Task.Run(async () => await UpdateProductAsync(context, 1, updateAction1));

        synchronizationEvent.Set();
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        taskEvents[1].Set();
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        taskEvents[0].Set();

        await task0;
        await task1;

        var product = await GetAndPrintProductProperties(context);
        product.Title.Should().Be("Cucumber-0");
        product.VersionNumber.Should().Be(3);
        
        await context.DisposeAsync();
    }
    
    [Fact]
    public async Task UpdatingMultipleProperties_DoesNotLeadToCorruptValues()
    {
        var context = new TestContext();
        await context.SeedAsync(initialProduct);
        Action<ConcurrentProduct, ProductDbContext> updateAction0 = (p, _) =>
        {
            p.Title = "Cucumber";
            p.Price = 1.99m;
            ++p.VersionNumber;
        };
        Action<ConcurrentProduct, ProductDbContext> updateAction1 = (p, _) =>
        {
            p.Title = "Cucumber-1";
            p.Price = 0.99m;
            ++p.VersionNumber;
        };

        var task0 = Task.Run(async () => await UpdateProductAsync(context, 0, updateAction0));
        var task1 = Task.Run(async () => await UpdateProductAsync(context, 1, updateAction1));

        synchronizationEvent.Set();
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        taskEvents[0].Set();
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        taskEvents[1].Set();

        await task0;
        await task1;

        var product = await GetAndPrintProductProperties(context);
        product.Title.Should().Be("Cucumber-1");
        product.Price.Should().Be(0.99m);
        
        await context.DisposeAsync();
    }

    private async Task<ConcurrentProduct> GetAndPrintProductProperties(TestContext context)
    {
        var dbContext = context.GetRequiredService<ProductDbContext>();
        var existingProduct = await dbContext.Set<ConcurrentProduct>().FindAsync(initialProduct.Id);
        output.WriteLine($"Title: {existingProduct!.Title}");
        output.WriteLine($"Price: {existingProduct.Price}");
        output.WriteLine($"VersionNo: {existingProduct.VersionNumber}");
        return existingProduct;
    }

    private async Task UpdateProductAsync(TestContext context, int taskId, Action<ConcurrentProduct, ProductDbContext> updateAction)
    {
        var isRetrying = false;
        do
        {
            var dbContext = context.GetRequiredService<ProductDbContext>();
            var existingProduct = await dbContext.Set<ConcurrentProduct>().FindAsync(initialProduct.Id);

            output.WriteLine($"Task {taskId} retrieved product");
            if (!isRetrying)
            {
                synchronizationEvent.WaitOne();
            }
            output.WriteLine($"Task {taskId} performing update");
            updateAction(existingProduct!, dbContext);
            if (!isRetrying)
            {
                taskEvents[taskId].WaitOne();
            }
            
            output.WriteLine($"Task {taskId} saving");
            try
            {
                await dbContext.SaveChangesAsync();
                isRetrying = false;
            }
            catch (DbUpdateConcurrencyException)
            {
                output.WriteLine($"Task {taskId} could not save due to DbUpdateConcurrencyException");
                isRetrying = true;
            }
        } 
        while (isRetrying);
        
    }
}