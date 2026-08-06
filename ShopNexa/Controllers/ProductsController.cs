using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopNexa.Data;
using ShopNexa.Services;

namespace ShopNexa.Controllers;

public class ProductsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProductsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? categoryId, string? q, decimal? minPrice, decimal? maxPrice, int? minRating, string? shipping)
    {
        var query = _context.Products.Include(p => p.Category).AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(p => p.Name.Contains(q) || (p.Description != null && p.Description.Contains(q)));
        }

        if (minPrice.HasValue)
        {
            query = query.Where(p => p.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= maxPrice.Value);
        }

        // Note: Rating filtering would require a Rating field in Product model
        // For now, we'll skip rating filtering

        var products = await query.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        ViewBag.Categories = await _context.Categories.AsNoTracking().ToListAsync();
        ViewBag.SelectedCategory = categoryId;
        ViewBag.Search = q;
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.MinRating = minRating;
        ViewBag.Shipping = shipping;
        return View(products);
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Seller)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();

        return View(product);
    }
}
