namespace ConcurrencyTests;

public class SimpleProduct
{
    public int Id { get; init; }
    public string Title { get; set; } = null!;
    public decimal Price { get; set; }
}