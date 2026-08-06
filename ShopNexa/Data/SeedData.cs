using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShopNexa.Models;

namespace ShopNexa.Data;

public static class SeedData
{
    public static async Task InitializeAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        // Create roles if not exists
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        if (!await roleManager.RoleExistsAsync("Seller"))
        {
            await roleManager.CreateAsync(new IdentityRole("Seller"));
        }

        // Create default admin if not exists
        if (await userManager.FindByEmailAsync("zenithshop18@gmail.com") is null)
        {
            var admin = new ApplicationUser
            {
                UserName = "zenithshop18@gmail.com",
                Email = "zenithshop18@gmail.com",
                FullName = "ZenithShop Admin",
                EmailConfirmed = true
            };

            await userManager.CreateAsync(admin, "Mahadev@009");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        // Get or create categories
        List<Category> categories;

        if (!context.Categories.Any())
        {
            categories = new List<Category>
            {
                new() { Name = "Electronics", Description = "Smart devices for everyday life", ImageUrl = "https://images.unsplash.com/photo-1518779578993-ec3579fee39f?auto=format&fit=crop&w=800&q=80" },
                new() { Name = "Fashion", Description = "Style that fits you", ImageUrl = "https://images.unsplash.com/photo-1521572267360-ee0c2909d518?auto=format&fit=crop&w=800&q=80" },
                new() { Name = "Home & Living", Description = "Make your home cozy", ImageUrl = "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=800&q=80" },
                new() { Name = "Sports & Fitness", Description = "Gear to keep you moving", ImageUrl = "https://images.unsplash.com/photo-1521412644187-c49fa049e84d?auto=format&fit=crop&w=800&q=80" },
            };

            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();
        }
        else
        {
            categories = await context.Categories.ToListAsync();
        }

        // ✅ IMPORTANT FIX:
        // Only seed products if database is EMPTY
        if (!context.Products.Any())
        {
            var products = new List<Product>
            {
                new() { Name = "boAt Airdopes 141 Bluetooth Earbuds", Description = "42H Playtime, ENx™ Tech, 8MM Drivers, IWP™ Technology, IPX4 Water Resistance, ASAP™ Charge", Price = 999.00m, OriginalPrice = 1499.00m, Stock = 120, CategoryId = categories[0].Id, ImageUrl = "https://images.unsplash.com/photo-1590658268037-6bf12165a8df?w=800&h=800&fit=crop&auto=format" },
                new() { Name = "Fire-Boltt Ninja 3 Smart Watch", Description = "1.69\" Display, 60 Sports Modes, 24*7 Heart Rate Monitor, SpO2, Sleep Tracking, IP67 Waterproof", Price = 1299.00m, OriginalPrice = 1999.00m, Stock = 85, CategoryId = categories[0].Id, ImageUrl = "https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=800&h=800&fit=crop&auto=format" },
                new() { Name = "Samsung Galaxy Buds2 Pro", Description = "Active Noise Cancellation, 360 Audio, IPX7 Water Resistant, 8 Hours Battery, Wireless Charging", Price = 8999.00m, OriginalPrice = 12999.00m, Stock = 45, CategoryId = categories[0].Id, ImageUrl = "https://images.unsplash.com/photo-1590658268037-6bf12165a8df?w=800&h=800&fit=crop&auto=format" },
                new() { Name = "JBL Flip 6 Portable Speaker", Description = "12 Hours Playtime, IPX7 Waterproof, PartyBoost, JBL Pro Sound, USB Type-C Charging", Price = 6999.00m, OriginalPrice = 9999.00m, Stock = 60, CategoryId = categories[0].Id, ImageUrl = "https://images.unsplash.com/photo-1608043152269-423dbba4e7e1?w=800&h=800&fit=crop&auto=format" },
                new() { Name = "OnePlus Nord Buds 2r", Description = "12.4mm Drivers, 30H Battery, Fast Charging, IPX4 Water Resistant, Dolby Atmos", Price = 1999.00m, OriginalPrice = 2999.00m, Stock = 90, CategoryId = categories[0].Id, ImageUrl = "https://images.unsplash.com/photo-1590658268037-6bf12165a8df?w=800&h=800&fit=crop&auto=format" },
                new() { Name = "Mi Smart Band 8", Description = "1.62\" AMOLED Display, 14 Days Battery, 150+ Sports Modes, 5ATM Water Resistant, Sleep Tracking", Price = 2499.00m, OriginalPrice = 3499.00m, Stock = 75, CategoryId = categories[0].Id, ImageUrl = "https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=800&h=800&fit=crop&auto=format" }
            };

            context.Products.AddRange(products);
            await context.SaveChangesAsync();
        }
    }
}
