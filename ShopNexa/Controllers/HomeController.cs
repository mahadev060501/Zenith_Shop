using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopNexa.Data;
using ShopNexa.Models;
using System.Diagnostics;

namespace ShopNexa.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(
        ILogger<HomeController> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> CreateAdmin()
    {
        var user = await _userManager.FindByEmailAsync("admin@zenithshop.com");

        if (user == null)
        {
            var admin = new ApplicationUser
            {
                UserName = "admin@zenithshop.com",
                Email = "admin@zenithshop.com",
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(admin, "Admin@123");

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(admin, "Admin");
                return Content("Admin created successfully.");
            }

            return Content("Admin creation failed.");
        }

        return Content("Admin already exists.");
    }

    public async Task<IActionResult> Index()
    {
        // Get all products for carousel (up to 10)
        var carouselProducts = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.Stock > 0)
            .OrderByDescending(p => p.Id)
            .Take(10)
            .ToListAsync();

        // Create hero slides from all products
        var heroSlides = new List<HeroSlide>();
        
        foreach (var product in carouselProducts)
        {
            var slide = new HeroSlide
            {
                Title = product.Name,
                Subtitle = product.Category?.Name?.ToUpper() ?? "FEATURED",
                Description = product.Description != null && product.Description.Length > 100 
                    ? product.Description[..100] + "..." 
                    : product.Description ?? "Check out this amazing product!",
                ProductImageUrl = product.ImageUrl,
                CtaText = "Buy Now",
                CtaLink = $"/Products/Details/{product.Id}"
            };

            // Add sale badge if product is on sale
            if (product.OriginalPrice.HasValue && product.OriginalPrice > product.Price)
            {
                var discountPercent = (int)(((product.OriginalPrice.Value - product.Price) / product.OriginalPrice.Value) * 100);
                slide.BadgeText = $"-{discountPercent}%";
                slide.DiscountText = $"₹{product.OriginalPrice.Value.ToString("N0")} ₹{product.Price.ToString("N0")}";
            }
            else
            {
                slide.BadgeText = "NEW";
                slide.DiscountText = $"₹{product.Price.ToString("N0")}";
            }

            heroSlides.Add(slide);
        }

        // Add default promotional slide if no products
        if (!heroSlides.Any())
        {
            heroSlides.Add(new HeroSlide
            {
                Title = "Welcome to ShopNexa",
                Subtitle = "SHOP NOW",
                Description = "Discover amazing products at great prices. Start shopping today!",
                CtaText = "Browse Products",
                CtaLink = "/Products",
                BadgeText = "NEW"
            });
        }

        // Get categories with their newest product image (ordered by Id descending)
        var categoriesWithProducts = await _context.Categories
            .Where(c => c.Products.Any(p => p.Stock > 0))
            .Select(c => new CategoryWithProduct
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ImageUrl = c.ImageUrl,
                ProductCount = c.Products.Count(p => p.Stock > 0),
                ProductImageUrl = c.Products
                    .Where(p => p.Stock > 0 && !string.IsNullOrEmpty(p.ImageUrl))
                    .OrderByDescending(p => p.Id)
                    .Select(p => p.ImageUrl)
                    .FirstOrDefault()
                    ?? c.Products
                        .Where(p => p.Stock > 0)
                        .OrderByDescending(p => p.Id)
                        .Select(p => p.ImageUrl)
                        .FirstOrDefault()
            })
            .Take(8)
            .ToListAsync();

        var viewModel = new HomeViewModel
        {
            FeaturedProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.Stock > 0)
                .OrderByDescending(p => p.Id)
                .Take(8)
                .ToListAsync(),
            Categories = categoriesWithProducts,
            ProductsOnSale = carouselProducts.Where(p => p.OriginalPrice.HasValue && p.OriginalPrice > p.Price).ToList(),
            HeroSlides = heroSlides
        };
        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
