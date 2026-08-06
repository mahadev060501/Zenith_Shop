namespace ShopNexa.Models;

public class CartItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
    public ApplicationUser? User { get; set; }
}

