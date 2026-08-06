using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopNexa.Models;

public class Product
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Range(0, 999999)]
    public decimal Price { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Range(0, 999999)]
    public decimal? OriginalPrice { get; set; }

    [Range(0, 100000)]
    public int Stock { get; set; }

    [Url]
    public string? ImageUrl { get; set; }

    [Required]
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public string? SellerId { get; set; }
    public ApplicationUser? Seller { get; set; }

    // Additional metadata used for richer UI scenarios. These are not persisted yet to
    // avoid schema mismatches in existing databases.
    [NotMapped]
    public int StockQuantity
    {
        get => Stock;
        set => Stock = value;
    }

    [NotMapped]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public bool IsActive { get; set; } = true;
}

