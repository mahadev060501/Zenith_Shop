using System.ComponentModel.DataAnnotations;

namespace ShopNexa.Models;

public class Category
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Description { get; set; }

    [Url]
    public string? ImageUrl { get; set; }

    public List<Product> Products { get; set; } = new();
}

