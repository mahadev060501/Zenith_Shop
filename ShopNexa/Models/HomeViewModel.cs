namespace ShopNexa.Models;

public class HomeViewModel
{
    public List<CategoryWithProduct> Categories { get; set; } = new();
    public List<Product> FeaturedProducts { get; set; } = new();
    public List<Product> ProductsOnSale { get; set; } = new();
    public List<HeroSlide> HeroSlides { get; set; } = new();
}

public class CategoryWithProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int ProductCount { get; set; }
    public string? ProductImageUrl { get; set; }
}

public class HeroSlide
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ProductImageUrl { get; set; }
    public string CtaText { get; set; } = "Shop now";
    public string CtaLink { get; set; } = "/Products";
    public string? BadgeText { get; set; }
    public string? DiscountText { get; set; }
}

