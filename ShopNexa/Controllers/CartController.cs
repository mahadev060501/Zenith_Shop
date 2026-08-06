using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopNexa.Data;
using ShopNexa.Models;
using ShopNexa.Services;
using System.Security.Claims;

namespace ShopNexa.Controllers;

[Authorize]
public class CartController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CartService _cartService;

    public CartController(ApplicationDbContext context, CartService cartService)
    {
        _context = context;
        _cartService = cartService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Count()
    {
        if (User?.Identity?.IsAuthenticated != true)
            return Json(new { count = 0 });

        var count = await _cartService.GetCartCountAsync();
        return Json(new { count });
    }

    public async Task<IActionResult> Index()
    {
        var cart = await _cartService.GetCartAsync();
        ViewBag.Total = await _cartService.GetCartTotalAsync();
        return View(cart);
    }

    [HttpPost]
    public async Task<IActionResult> Add(int productId, int quantity = 1)
    {
        if (quantity < 1)
            quantity = 1;

        var product = await _context.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId);

        if (product == null)
            return Json(new { success = false, message = "Product not found." });

        if (product.Stock < quantity)
            return Json(new { success = false, message = $"Only {product.Stock} item(s) available in stock." });

        // Check if adding would exceed stock
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var existing = await _context.CartItems
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

        var currentQty = existing?.Quantity ?? 0;
        if (product.Stock < currentQty + quantity)
            return Json(new { success = false, message = $"Only {product.Stock} item(s) available in stock." });

        await _cartService.AddToCartAsync(productId, quantity);
        var count = await _cartService.GetCartCountAsync();

        return Json(new { success = true, count });
    }

    [HttpPost]
    public async Task<IActionResult> BuyNow(int productId, int quantity = 1)
    {
        var product = await _context.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId);

        if (product == null) return NotFound();

        if (product.Stock < quantity)
        {
            TempData["Error"] = $"Only {product.Stock} items available in stock.";
            return RedirectToAction("Details", "Products", new { id = productId });
        }

        // Clear existing cart and add only this item
        await _cartService.ClearCartAsync();
        await _cartService.AddToCartAsync(productId, quantity);

        return RedirectToAction("Index", "Checkout");
    }

    [HttpPost]
    public async Task<IActionResult> Update(int productId, int quantity)
    {
        await _cartService.UpdateQuantityAsync(productId, quantity);
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Remove(int productId)
    {
        await _cartService.RemoveFromCartAsync(productId);
        TempData["Success"] = "Item removed from cart.";
        return RedirectToAction("Index");
    }
}

