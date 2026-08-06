using Microsoft.EntityFrameworkCore;
using ShopNexa.Data;
using ShopNexa.Models;
using System.Security.Claims;

namespace ShopNexa.Services;

public class CartService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CartService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    private string? GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    public async Task<List<CartItem>> GetCartAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return new List<CartItem>();

        return await _context.CartItems
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .ToListAsync();
    }

    public async Task<int> GetCartCountAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return 0;

        return await _context.CartItems
            .Where(c => c.UserId == userId)
            .SumAsync(c => c.Quantity);
    }

    public async Task<decimal> GetCartTotalAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return 0;

        return await _context.CartItems
            .Where(c => c.UserId == userId)
            .SumAsync(c => c.Price * c.Quantity);
    }

    public async Task AddToCartAsync(int productId, int quantity)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("User must be authenticated");

        var existing = await _context.CartItems
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

        if (existing != null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) throw new InvalidOperationException("Product not found");

            _context.CartItems.Add(new CartItem
            {
                UserId = userId,
                ProductId = productId,
                Name = product.Name,
                Price = product.Price,
                Quantity = quantity,
                ImageUrl = product.ImageUrl,
                AddedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task UpdateQuantityAsync(int productId, int quantity)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return;

        var item = await _context.CartItems
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

        if (item != null)
        {
            item.Quantity = Math.Max(1, quantity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveFromCartAsync(int productId)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return;

        var items = await _context.CartItems
            .Where(c => c.UserId == userId && c.ProductId == productId)
            .ToListAsync();

        _context.CartItems.RemoveRange(items);
        await _context.SaveChangesAsync();
    }

    public async Task ClearCartAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return;

        var items = await _context.CartItems
            .Where(c => c.UserId == userId)
            .ToListAsync();

        _context.CartItems.RemoveRange(items);
        await _context.SaveChangesAsync();
    }

    public async Task ClearCartForUserAsync(string userId)
    {
        var items = await _context.CartItems
            .Where(c => c.UserId == userId)
            .ToListAsync();

        _context.CartItems.RemoveRange(items);
        await _context.SaveChangesAsync();
    }
}
