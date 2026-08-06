using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShopNexa.Data;
using ShopNexa.Models;

namespace ShopNexa.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> Dashboard()
    {
        ViewBag.ProductCount = await _context.Products.CountAsync();
        ViewBag.OrderCount = await _context.Orders.CountAsync();
        ViewBag.PendingOrders = await _context.Orders.CountAsync(o => o.Status == "Pending");
        ViewBag.CategoryCount = await _context.Categories.CountAsync();
        ViewBag.PendingSellerRequests = await _context.SellerRequests.CountAsync(sr => sr.Status == "Pending");
        ViewBag.TotalSellers = await _context.SellerProfiles.CountAsync();
        ViewBag.TotalReturned = await _context.Orders.CountAsync(o => o.Status == "Returned");
        ViewBag.PendingReturnRequests = await _context.Orders.CountAsync(o => o.Status == "Return Requested") +
            await _context.OrderItems.CountAsync(oi => oi.ReturnStatus == "Return Requested");
        return View();
    }

    public async Task<IActionResult> Products()
    {
        var products = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Seller)
            .ToListAsync();

        var sellerIds = products.Where(p => p.SellerId != null).Select(p => p.SellerId!).Distinct().ToList();
        var sellerProfiles = await _context.SellerProfiles
            .Where(sp => sellerIds.Contains(sp.UserId))
            .ToListAsync();
        ViewBag.SellerCompanyByUserId = sellerProfiles.ToDictionary(sp => sp.UserId, sp => sp.CompanyName);

        var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
        ViewBag.AdminUserIds = adminUsers.Select(u => u.Id).ToHashSet();

        ViewBag.Categories = await _context.Categories.ToListAsync();
        return View(products);
    }

    public async Task<IActionResult> CreateProduct()
    {
        ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name");
        return View(new Product());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(Product product)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", product.CategoryId);
            return View(product);
        }

        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Products));
    }

    public async Task<IActionResult> EditProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();
        ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", product.CategoryId);
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProduct(int id, Product product)
    {
        if (id != product.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", product.CategoryId);
            return View(product);
        }

        _context.Update(product);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Products));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Products));
    }

    public async Task<IActionResult> Categories()
    {
        var categories = await _context.Categories.ToListAsync();
        return View(categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(Category category)
    {
        if (ModelState.IsValid)
        {
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Categories));
        }
        return View("Categories", await _context.Categories.ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCategory(int id, Category category)
    {
        if (id != category.Id) return BadRequest();
        if (ModelState.IsValid)
        {
            _context.Update(category);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Categories));
        }
        return View("Categories", await _context.Categories.ToListAsync());
    }

    public async Task<IActionResult> Orders()
    {
        var orders = await _context.Orders.Include(o => o.Items).ThenInclude(i => i.Product).OrderByDescending(o => o.CreatedAt).ToListAsync();
        return View(orders);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveReturn(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            TempData["Error"] = "Order not found.";
            return RedirectToAction(nameof(Orders));
        }

        var hasReturnRequest = order.Status == "Return Requested" ||
                               (order.Items != null && order.Items.Any(i => i.ReturnStatus == "Return Requested"));

        if (!hasReturnRequest)
        {
            TempData["Info"] = "No pending return request for this order.";
            return RedirectToAction(nameof(Orders));
        }

        if (order.Status == "Return Requested")
        {
            order.Status = "Returned";
        }

        if (order.Items != null)
        {
            foreach (var item in order.Items.Where(i => i.ReturnStatus == "Return Requested"))
            {
                item.ReturnStatus = "Returned";
            }
        }

        if (order.PaymentStatus == "Paid")
        {
            if (string.IsNullOrEmpty(order.RefundStatus) || order.RefundStatus == "Not Applicable")
            {
                order.RefundStatus = "Completed";
                if (!order.RefundAmount.HasValue)
                {
                    order.RefundAmount = order.Total;
                }
                if (!order.RefundDate.HasValue)
                {
                    order.RefundDate = DateTime.UtcNow;
                }
            }
            else if (order.RefundStatus == "Processing" || order.RefundStatus == "Initiated")
            {
                order.RefundStatus = "Completed";
                if (!order.RefundDate.HasValue)
                {
                    order.RefundDate = DateTime.UtcNow;
                }
            }
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Return approved for order #{order.Id}.";
        return RedirectToAction(nameof(Orders));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectReturn(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            TempData["Error"] = "Order not found.";
            return RedirectToAction(nameof(Orders));
        }

        var hasReturnRequest = order.Status == "Return Requested" ||
                               (order.Items != null && order.Items.Any(i => i.ReturnStatus == "Return Requested"));

        if (!hasReturnRequest)
        {
            TempData["Info"] = "No pending return request for this order.";
            return RedirectToAction(nameof(Orders));
        }

        // Order-level return
        if (order.Status == "Return Requested")
        {
            if (order.Items != null)
            {
                foreach (var item in order.Items)
                {
                    if (item.ProductId > 0)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.Stock = Math.Max(0, product.Stock - item.Quantity);
                        }
                    }
                }
            }

            order.Status = "Delivered";
        }
        else if (order.Items != null)
        {
            // Item-level returns
            foreach (var item in order.Items.Where(i => i.ReturnStatus == "Return Requested"))
            {
                if (item.ProductId > 0)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.Stock = Math.Max(0, product.Stock - item.Quantity);
                    }
                }

                item.ReturnStatus = "Return Rejected";
                item.ReturnRequestDate = null;
                item.RefundAmount = null;
            }
        }

        if (order.PaymentStatus == "Paid")
        {
            order.RefundStatus = "Rejected";
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Return request rejected for order #{order.Id}.";
        return RedirectToAction(nameof(Orders));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order != null)
        {
            order.Status = status;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Orders));
    }

    // Removed SellerApplications method since we removed SellerApplication model

    // ==================== NEW SELLER REQUEST SYSTEM ====================

    // GET: Admin/SellerRequests - View all pending seller requests
    public async Task<IActionResult> SellerRequests()
    {
        var requests = await _context.SellerRequests
            .Include(sr => sr.User)
            .OrderByDescending(sr => sr.CreatedAt)
            .ToListAsync();

        return View(requests);
    }

    // POST: Admin/ApproveSellerRequest - Approve a seller request
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveSellerRequest(int id)
    {
        var request = await _context.SellerRequests
            .Include(sr => sr.User)
            .FirstOrDefaultAsync(sr => sr.Id == id);

        if (request == null)
        {
            TempData["Error"] = "Seller request not found.";
            return RedirectToAction(nameof(SellerRequests));
        }

        if (request.Status != "Pending")
        {
            TempData["Error"] = "This request has already been processed.";
            return RedirectToAction(nameof(SellerRequests));
        }

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 1. Update request status
            request.Status = "Approved";
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedBy = User.Identity?.Name;

            // 2. Create SellerProfile
            var sellerProfile = new SellerProfile
            {
                UserId = request.UserId,
                CompanyName = request.CompanyName,
                OwnerName = request.OwnerName,
                Email = request.Email,
                Phone = request.Phone,
                Address = request.Address,
                GSTNumber = request.GSTNumber,
                BankAccountDetails = request.BankAccountDetails,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.SellerProfiles.Add(sellerProfile);

            // 3. Update user role
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user != null)
            {
                // Remove from Customer role if exists
                if (await _userManager.IsInRoleAsync(user, "Customer"))
                {
                    await _userManager.RemoveFromRoleAsync(user, "Customer");
                }

                // Add to Seller role
                if (!await _userManager.IsInRoleAsync(user, "Seller"))
                {
                    if (!await _roleManager.RoleExistsAsync("Seller"))
                    {
                        await _roleManager.CreateAsync(new IdentityRole("Seller"));
                    }
                    await _userManager.AddToRoleAsync(user, "Seller");
                }

                // Update user flags
                user.IsSellerApproved = true;
                user.SellerApplicationDate = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = $"Seller request for {request.CompanyName} has been approved successfully.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = $"Error approving seller: {ex.Message}";
        }

        return RedirectToAction(nameof(SellerRequests));
    }

    // POST: Admin/RejectSeller - Reject a seller request
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectSeller(int id, string adminNotes)
    {
        var request = await _context.SellerRequests
            .FirstOrDefaultAsync(sr => sr.Id == id);

        if (request == null)
        {
            TempData["Error"] = "Seller request not found.";
            return RedirectToAction(nameof(SellerRequests));
        }

        request.Status = "Rejected";
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewedBy = User.Identity?.Name;
        request.AdminNotes = adminNotes;

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Seller request for {request.CompanyName} has been rejected.";
        return RedirectToAction(nameof(SellerRequests));
    }

    // GET: Admin/ManageSellers - View all approved sellers
    public async Task<IActionResult> ManageSellers()
    {
        var sellers = await _context.SellerProfiles
            .Include(sp => sp.User)
            .OrderByDescending(sp => sp.CreatedAt)
            .ToListAsync();

        return View(sellers);
    }

    // POST: Admin/ToggleSellerStatus - Activate/Deactivate seller
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSellerStatus(int id)
    {
        var seller = await _context.SellerProfiles.FindAsync(id);
        if (seller == null)
        {
            TempData["Error"] = "Seller not found.";
            return RedirectToAction(nameof(ManageSellers));
        }

        seller.IsActive = !seller.IsActive;
        seller.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var status = seller.IsActive ? "activated" : "deactivated";
        TempData["Success"] = $"Seller {seller.CompanyName} has been {status}.";
        return RedirectToAction(nameof(ManageSellers));
    }

    // POST: Admin/DeleteSeller - Delete seller profile
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSeller(int id)
    {
        var seller = await _context.SellerProfiles.FindAsync(id);
        if (seller == null)
        {
            TempData["Error"] = "Seller not found.";
            return RedirectToAction(nameof(ManageSellers));
        }

        // Revert user role back to Customer
        var user = await _userManager.FindByIdAsync(seller.UserId);
        if (user != null)
        {
            if (await _userManager.IsInRoleAsync(user, "Seller"))
            {
                await _userManager.RemoveFromRoleAsync(user, "Seller");
            }
            await _userManager.AddToRoleAsync(user, "Customer");
            user.IsSellerApproved = false;
            await _userManager.UpdateAsync(user);
        }

        _context.SellerProfiles.Remove(seller);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Seller {seller.CompanyName} has been deleted.";
        return RedirectToAction(nameof(ManageSellers));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Sellers()
    {
        var sellers = await _context.SellerProfiles
            .Include(sp => sp.User)
            .OrderByDescending(sp => sp.CreatedAt)
            .ToListAsync();

        return View(sellers);
    }

    public async Task<IActionResult> SellerDetails(int id)
    {
        var seller = await _context.SellerProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.Id == id);

        if (seller == null)
        {
            return NotFound();
        }

        return View(seller);
    }
}