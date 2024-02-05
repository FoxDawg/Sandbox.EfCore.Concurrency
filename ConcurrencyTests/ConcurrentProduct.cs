namespace ConcurrencyTests;

public class ConcurrentProduct
{
    public int Id { get; init; }
    public string Title { get; set; } = null!;
    public decimal Price { get; set; }
    public int VersionNumber { get; set; }
}