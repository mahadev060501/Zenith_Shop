using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShopNexa.Data;
using ShopNexa.Models;

namespace ShopNexa.Controllers;

[Authorize]
public class SellerController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<SellerController> _logger;

    public SellerController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<SellerController> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> BecomeSeller()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        // Check if user is already a seller
        var isInRole = await _userManager.IsInRoleAsync(user, "Seller");
        
        // If user is already a seller, redirect immediately
        if (isInRole && user.IsSellerApproved)
        {
            // Ensure role is added if missing
            if (!isInRole)
            {
                if (!await _roleManager.RoleExistsAsync("Seller"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Seller"));
                }
                await _userManager.AddToRoleAsync(user, "Seller");
                await _signInManager.RefreshSignInAsync(user);
            }
            
            // Force redirect to Dashboard
            return RedirectToAction("Dashboard", "Seller");
        }
        
        // Also check if user is approved but role wasn't added (edge case)
        if (user.IsSellerApproved && !isInRole)
        {
            if (!await _roleManager.RoleExistsAsync("Seller"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Seller"));
            }
            await _userManager.AddToRoleAsync(user, "Seller");
            await _signInManager.RefreshSignInAsync(user);
            return RedirectToAction("Dashboard", "Seller");
        }

        // User is not a seller yet - show landing page
        return View();
    }

    // GET: Seller/Register - Show registration form
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Register()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        // Already approved seller → dashboard
        if (await _userManager.IsInRoleAsync(user, "Seller") && user.IsSellerApproved)
        {
            return RedirectToAction("Dashboard");
        }

        // Check if request already exists
        var existingRequest = await _context.SellerRequests
            .FirstOrDefaultAsync(sr => sr.UserId == user.Id);

        if (existingRequest != null)
        {
            if (existingRequest.Status == "Pending")
            {
                TempData["Info"] = "Your seller request is pending approval.";
                return RedirectToAction("ApplicationStatus");
            }
            else if (existingRequest.Status == "Approved")
            {
                return RedirectToAction("Dashboard");
            }
            else if (existingRequest.Status == "Rejected")
            {
                TempData["Error"] = "Your previous seller request was rejected. You can apply again.";
            }
        }

        return View(new SellerRegisterViewModel());
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(SellerRegisterViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        if (!ModelState.IsValid)
            return View(model);

        var existingRequest = await _context.SellerRequests
            .FirstOrDefaultAsync(sr => sr.UserId == user.Id && sr.Status == "Pending");

        if (existingRequest != null)
        {
            TempData["Info"] = "You already have a pending seller request.";
            return RedirectToAction("ApplicationStatus");
        }

        var request = new SellerRequest
        {
            UserId = user.Id,
            CompanyName = model.CompanyName,
            OwnerName = model.OwnerName,
            Email = model.Email,
            Phone = model.Phone,
            Address = model.Address,
            GSTNumber = model.GSTNumber,
            BankAccountDetails = model.BankAccountDetails,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.SellerRequests.Add(request);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Your seller request has been submitted successfully.";
        return RedirectToAction("ApplicationStatus");
    }

    // GET: Seller/ApplicationStatus - Check request status
    [HttpGet]
    public async Task<IActionResult> ApplicationStatus()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var request = await _context.SellerRequests
            .Where(sr => sr.UserId == user.Id)
            .OrderByDescending(sr => sr.CreatedAt)
            .FirstOrDefaultAsync();

        if (request == null)
        {
            return RedirectToAction("BecomeSeller");
        }

        return View(request);
    }

    // This method is no longer needed since we removed SellerApplication
    // We'll keep the BecomeSeller GET method only

    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        // IMPORTANT: Reload user from database to ensure we have latest data (especially after registration)
        // This ensures that if user just registered, we get the updated IsSellerApproved flag
        user = await _userManager.FindByIdAsync(user.Id);
        if (user == null) return NotFound();

        // IMPORTANT: This page is ONLY for sellers. If user is not a seller, redirect to registration.
        // First check if user is approved
        if (!user.IsSellerApproved)
        {
            TempData["Error"] = "You are not registered as a seller. Please register first to access the dashboard.";
            return RedirectToAction("BecomeSeller", "Seller");
        }

        // If approved, ensure they have the role
        var hasRole = await _userManager.IsInRoleAsync(user, "Seller");
        if (!hasRole)
        {
            // User is approved but missing role - add it immediately
            if (!await _roleManager.RoleExistsAsync("Seller"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Seller"));
            }
            await _userManager.AddToRoleAsync(user, "Seller");
            await _userManager.UpdateAsync(user);
            await _context.SaveChangesAsync();
            
            // Reload user from database to get latest data
            user = await _userManager.FindByIdAsync(user.Id);
            if (user == null) return NotFound();
            
            await _signInManager.RefreshSignInAsync(user);
            
            // Re-check after adding role
            hasRole = await _userManager.IsInRoleAsync(user, "Seller");
        }

        // If still no role after all attempts, redirect to registration
        if (!hasRole)
        {
            TempData["Error"] = "There was an issue setting up your seller account. Please try registering again.";
            return RedirectToAction("BecomeSeller", "Seller");
        }

        var products = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.SellerId != null && p.SellerId == user.Id)
            .ToListAsync();

        var orderItems = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product)
            .Where(oi => oi.Product != null && oi.Product.SellerId != null && oi.Product.SellerId == user.Id && oi.Order != null)
            .ToListAsync();

        var orderGroups = orderItems
            .GroupBy(oi => oi.OrderId)
            .ToList();

        var totalRevenue = orderItems.Sum(oi => oi.Quantity * oi.UnitPrice);
        var confirmedOrders = orderGroups.Count(g =>
        {
            var firstItem = g.First();
            return firstItem.Order != null && firstItem.Order.Status == "Confirmed";
        });
        var deliveredOrders = orderGroups.Count(g =>
        {
            var firstItem = g.First();
            return firstItem.Order != null && firstItem.Order.Status == "Delivered";
        });
        var pendingOrders = orderGroups.Count(g =>
        {
            var firstItem = g.First();
            return firstItem.Order != null && firstItem.Order.Status == "Pending";
        });

        // Monthly revenue (current month)
        var currentMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var monthlyRevenue = orderItems
            .Where(oi => oi.Order != null && oi.Order.CreatedAt >= currentMonthStart)
            .Sum(oi => oi.Quantity * oi.UnitPrice);

        // Average order value
        var averageOrderValue = orderGroups.Count > 0 
            ? orderGroups.Average(g => g.Sum(oi => oi.Quantity * oi.UnitPrice))
            : 0;

        // Low stock products (stock < 10)
        var lowStockProducts = products.Count(p => p.Stock > 0 && p.Stock < 10);

        // Recent orders (last 5)
        var recentOrders = orderGroups
            .OrderByDescending(g => g.First().Order?.CreatedAt ?? DateTime.MinValue)
            .Take(5)
            .Select(g => new RecentOrderViewModel
            {
                OrderId = g.First().Order?.Id ?? 0,
                CustomerName = g.First().Order?.CustomerName ?? "Unknown",
                OrderDate = g.First().Order?.CreatedAt ?? DateTime.MinValue,
                ItemCount = g.Count(),
                Total = g.Sum(oi => oi.Quantity * oi.UnitPrice),
                Status = g.First().Order?.Status ?? "Unknown"
            })
            .ToList();

        // Top selling products - calculate sold count from order items
        var productSoldCounts = orderItems
            .GroupBy(oi => oi.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(oi => oi.Quantity));

        var topProducts = products
            .Select(p => new TopProductViewModel
            {
                ProductId = p.Id,
                ProductName = p.Name,
                Price = p.Price,
                Stock = p.Stock,
                SoldCount = productSoldCounts.ContainsKey(p.Id) ? productSoldCounts[p.Id] : 0,
                ImageUrl = p.ImageUrl
            })
            .OrderByDescending(p => p.SoldCount)
            .Take(5)
            .ToList();

        // Total customers (unique customers who ordered from this seller)
        var totalCustomers = orderItems
            .Select(oi => oi.Order?.Email)
            .Where(email => !string.IsNullOrEmpty(email))
            .Distinct()
            .Count();

        // Cancelled orders
        var cancelledOrders = orderGroups.Count(g =>
        {
            var firstItem = g.First();
            return firstItem.Order != null && firstItem.Order.Status == "Cancelled";
        });

        // Out of stock products
        var outOfStockProducts = products.Count(p => p.Stock == 0);

        var stats = new SellerDashboardStats
        {
            TotalProducts = products.Count,
            TotalOrders = orderGroups.Count,
            PendingOrders = pendingOrders,
            ConfirmedOrders = confirmedOrders,
            DeliveredOrders = deliveredOrders,
            CancelledOrders = cancelledOrders,
            TotalRevenue = totalRevenue,
            MonthlyRevenue = monthlyRevenue,
            AverageOrderValue = averageOrderValue,
            LowStockProducts = lowStockProducts,
            OutOfStockProducts = outOfStockProducts,
            TotalCustomers = totalCustomers,
            RecentOrders = recentOrders,
            TopProducts = topProducts
        };

        return View(stats);
    }

    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> Products()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        // Verify user has Seller role
        if (!await _userManager.IsInRoleAsync(user, "Seller"))
        {
            TempData["Error"] = "You are not registered as a seller.";
            return RedirectToAction(nameof(BecomeSeller));
        }

        var products = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.SellerId != null && p.SellerId == user.Id)
            .ToListAsync();

        ViewBag.Categories = await _context.Categories.ToListAsync();
        return View(products);
    }

    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> CreateProduct()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        // Verify user has Seller role
        if (!await _userManager.IsInRoleAsync(user, "Seller"))
        {
            TempData["Error"] = "You are not registered as a seller.";
            return RedirectToAction(nameof(BecomeSeller));
        }

        ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name");
        return View(new Product());
    }

    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(Product product)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // Verify user has Seller role
        if (!await _userManager.IsInRoleAsync(user, "Seller"))
        {
            TempData["Error"] = "You are not registered as a seller.";
            return RedirectToAction(nameof(BecomeSeller));
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", product.CategoryId);
            return View(product);
        }

        // Defensive user verification
        var existingUser = await _userManager.FindByIdAsync(user.Id);
        if (existingUser == null)
        {
            throw new Exception("User not found in database.");
        }

        // Explicitly assign SellerId for every new product
        product.SellerId = existingUser.Id;

        if (string.IsNullOrEmpty(product.SellerId))
        {
            throw new Exception("SellerId was not assigned properly.");
        }

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Check if this is seller's first product
        var productCount = await _context.Products.CountAsync(p => p.SellerId != null && p.SellerId == user.Id);
        
        if (productCount == 1)
        {
            TempData["Success"] = "🎉 Step 2 & 3 Complete! Your product is now live and customers can start ordering! View your dashboard to track sales and revenue.";
            // Step 4: Redirect to Dashboard (Get Paid - track orders and earnings)
            return RedirectToAction(nameof(Dashboard));
        }
        else
        {
            TempData["Success"] = "Product created successfully! Your product is now live.";
            return RedirectToAction(nameof(Products));
        }
    }

    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> EditProduct(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        // STRICT SECURITY: Check if seller is approved
        if (!user.IsSellerApproved)
        {
            TempData["Error"] = "Your seller account is pending approval.";
            return RedirectToAction(nameof(ApplicationStatus));
        }

        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();

        // Ensure the product belongs to the current seller
        if (product.SellerId != user.Id)
        {
            TempData["Error"] = "You don't have permission to edit this product.";
            return RedirectToAction(nameof(Products));
        }

        ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", product.CategoryId);
        return View(product);
    }

    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProduct(int id, Product product)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        // STRICT SECURITY: Check if seller is approved
        if (!user.IsSellerApproved)
        {
            TempData["Error"] = "Your seller account is pending approval.";
            return RedirectToAction(nameof(ApplicationStatus));
        }

        if (id != product.Id) return BadRequest();

        var existingProduct = await _context.Products.FindAsync(id);
        if (existingProduct == null) return NotFound();

        // Ensure the product belongs to the current seller
        if (existingProduct.SellerId != user.Id)
        {
            TempData["Error"] = "You don't have permission to edit this product.";
            return RedirectToAction(nameof(Products));
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", product.CategoryId);
            return View(product);
        }

        existingProduct.Name = product.Name;
        existingProduct.Description = product.Description;
        existingProduct.Price = product.Price;
        existingProduct.Stock = product.Stock;
        existingProduct.ImageUrl = product.ImageUrl;
        existingProduct.CategoryId = product.CategoryId;

        await _context.SaveChangesAsync();
        TempData["Success"] = "Product updated successfully!";
        return RedirectToAction(nameof(Products));
    }

    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        // STRICT SECURITY: Check if seller is approved
        if (!user.IsSellerApproved)
        {
            TempData["Error"] = "Your seller account is pending approval.";
            return RedirectToAction(nameof(ApplicationStatus));
        }

        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();

        // Ensure the product belongs to the current seller
        if (product.SellerId != user.Id)
        {
            TempData["Error"] = "You don't have permission to delete this product.";
            return RedirectToAction(nameof(Products));
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Product deleted successfully!";
        return RedirectToAction(nameof(Products));
    }

    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> Orders(string? status = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        // STRICT SECURITY: Check if seller is approved
        if (!user.IsSellerApproved)
        {
            TempData["Error"] = "Your seller account is pending approval.";
            return RedirectToAction(nameof(ApplicationStatus));
        }

        var orderItems = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product)
            .Where(oi => oi.Product != null && oi.Product.SellerId != null && oi.Product.SellerId == user.Id)
            .OrderByDescending(oi => oi.Order != null ? oi.Order.CreatedAt : DateTime.MinValue)
            .ToListAsync();

        var orderIds = orderItems.Select(oi => oi.OrderId).Distinct().ToList();

        var orders = await _context.Orders
            .Include(o => o.Items)
            .Where(o => orderIds.Contains(o.Id))
            .ToListAsync();

        // Filter by status if provided
        if (!string.IsNullOrEmpty(status))
        {
            orders = orders.Where(o => o.Status == status).ToList();
        }

        return View(orders);
    }

    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> OrderDetails(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        // Verify seller owns at least one item in this order
        var sellerItems = order.Items.Where(oi => oi.Product?.SellerId == user.Id).ToList();
        if (!sellerItems.Any())
        {
            TempData["Error"] = "You don't have permission to view this order.";
            return RedirectToAction(nameof(Orders));
        }

        ViewBag.SellerItems = sellerItems;
        ViewBag.SellerTotal = sellerItems.Sum(oi => oi.Quantity * oi.UnitPrice);

        return View(order);
    }

    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> Customers()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        // STRICT SECURITY: Check if seller is approved
        if (!user.IsSellerApproved)
        {
            TempData["Error"] = "Your seller account is pending approval.";
            return RedirectToAction(nameof(ApplicationStatus));
        }

        var orderItems = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product)
            .Where(oi => oi.Product != null && oi.Product.SellerId != null && oi.Product.SellerId == user.Id)
            .ToListAsync();

        var customers = orderItems
            .Where(oi => oi.Order != null)
            .GroupBy(oi => oi.Order!.Email)
            .Select(g => new
            {
                Email = g.Key,
                Name = g.First().Order!.CustomerName,
                OrderCount = g.Select(oi => oi.OrderId).Distinct().Count(),
                TotalSpent = g.Sum(oi => oi.Quantity * oi.UnitPrice),
                LastOrder = g.Max(oi => oi.Order!.CreatedAt)
            })
            .OrderByDescending(c => c.TotalSpent)
            .ToList();

        return View(customers);
    }

}