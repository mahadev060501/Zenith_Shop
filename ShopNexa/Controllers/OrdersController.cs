using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopNexa.Data;
using ShopNexa.Models;

namespace ShopNexa.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrdersController>? _logger;

    public OrdersController(ApplicationDbContext context, ILogger<OrdersController>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Generates a unique refund transaction ID for Razorpay refunds.
    /// </summary>
    private string GenerateRefundTransactionId()
    {
        return $"REF{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
    }

    [HttpGet]
    public async Task<IActionResult> Success(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

        if (order == null)
        {
            return NotFound();
        }

        return View(order);
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        // Early return if userId is null
        if (string.IsNullOrEmpty(userId))
        {
            _logger?.LogWarning("User ID is null when trying to load orders");
            ViewBag.TotalItemsOrdered = 0;
            ViewBag.TotalOrders = 0;
            return View(new List<Order>());
        }
        
        List<Order> orders = new List<Order>();
        
        try
        {
            // Load orders with defensive null handling
            // Use AsNoTracking to avoid change tracking issues with null values
            var ordersQuery = _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId != null && o.UserId == userId);
            
            // Try to load orders - if it fails due to NULL values in database, catch and return empty
            try
            {
                orders = await ordersQuery.ToListAsync();
            }
            catch (InvalidOperationException nullEx) when (nullEx.Message.Contains("Null") || nullEx.Message.Contains("null"))
            {
                // Database has NULL values in required fields - return empty list
                // User should run Database/FixAllNullValues.sql
                _logger?.LogWarning(nullEx, "Orders table contains NULL values in required fields. Run Database/FixAllNullValues.sql to fix.");
                ViewBag.TotalItemsOrdered = 0;
                ViewBag.TotalOrders = 0;
                return View(new List<Order>());
            }
            catch (Exception queryEx)
            {
                // Any other query error - log and return empty
                _logger?.LogWarning(queryEx, "Error querying orders table. This may indicate NULL values in required fields.");
                ViewBag.TotalItemsOrdered = 0;
                ViewBag.TotalOrders = 0;
                return View(new List<Order>());
            }

            // If no orders, return early
            if (!orders.Any())
            {
                ViewBag.TotalItemsOrdered = 0;
                ViewBag.TotalOrders = 0;
                return View(orders);
            }

            // Ensure all required fields have defaults
            foreach (var order in orders)
            {
                if (string.IsNullOrEmpty(order.CustomerName)) order.CustomerName = "Unknown Customer";
                if (string.IsNullOrEmpty(order.Email)) order.Email = "";
                if (string.IsNullOrEmpty(order.AddressLine1)) order.AddressLine1 = "";
                if (string.IsNullOrEmpty(order.City)) order.City = "";
                if (string.IsNullOrEmpty(order.Country)) order.Country = "";
                if (string.IsNullOrEmpty(order.Status)) order.Status = "Pending";
                if (string.IsNullOrEmpty(order.PaymentMethod)) order.PaymentMethod = "COD";
                if (order.CreatedAt == default(DateTime)) order.CreatedAt = DateTime.UtcNow;
                if (order.Items == null) order.Items = new List<OrderItem>();
            }

            // Now load Items for each order separately to handle missing Products gracefully
            var orderIds = orders.Select(o => o.Id).ToList();
            
            if (orderIds.Any())
            {
                var orderItems = await _context.OrderItems
                    .Where(oi => orderIds.Contains(oi.OrderId))
                    .ToListAsync();

                // Load Products for items that have valid ProductIds
                var productIds = orderItems
                    .Where(oi => oi.ProductId > 0)
                    .Select(oi => oi.ProductId)
                    .Distinct()
                    .ToList();
                
                var products = productIds.Any() 
                    ? await _context.Products
                        .Where(p => productIds.Contains(p.Id))
                        .ToListAsync()
                    : new List<Product>();

                // Group items by order and attach products
                var itemsByOrder = orderItems.GroupBy(oi => oi.OrderId).ToDictionary(g => g.Key, g => g.ToList());
                
                foreach (var order in orders)
                {
                    if (itemsByOrder.TryGetValue(order.Id, out var items))
                    {
                        // Attach products to items
                        foreach (var item in items)
                        {
                            if (item.Product == null && item.ProductId > 0)
                            {
                                item.Product = products.FirstOrDefault(p => p.Id == item.ProductId);
                            }
                        }
                        order.Items = items;
                    }
                    else
                    {
                        order.Items = new List<OrderItem>();
                    }
                }
            }

            // Calculate total items ordered across all orders with null safety
            var totalItemsOrdered = orders
                .Where(o => o.Items != null)
                .Sum(o => o.Items.Sum(i => i.Quantity));
            
            ViewBag.TotalItemsOrdered = totalItemsOrdered;
            ViewBag.TotalOrders = orders.Count;

            return View(orders);
        }
        catch (Exception ex)
        {
            // Log the actual error for debugging
            _logger?.LogError(ex, "Error loading orders for user {UserId}. Exception: {ExceptionMessage}. StackTrace: {StackTrace}", 
                userId, ex.Message, ex.StackTrace);
            
            // Silently return empty list - don't show error to user if it's a data issue
            // The view will show "No orders yet" message
            ViewBag.TotalItemsOrdered = 0;
            ViewBag.TotalOrders = 0;
            return View(new List<Order>());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(int id, string? reason)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userId))
        {
            TempData["Error"] = "Unable to identify user. Please log in again.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction(nameof(Index));
            }

            // Only allow cancellation if order is not already cancelled, delivered, or refunded
            if (order.Status == "Cancelled" || order.Status == "Delivered" || order.Status == "Refunded" || 
                order.Status == "Return Requested" || order.Status == "Returned")
            {
                TempData["Error"] = "This order cannot be cancelled.";
                return RedirectToAction(nameof(Index));
            }

            // Update order status
            order.Status = "Cancelled";

            // Restore stock
            if (order.Items != null)
            {
                foreach (var item in order.Items)
                {
                    if (item.ProductId > 0)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.Stock += item.Quantity;
                        }
                    }
                }
            }

            // Process refund if payment was made (Flipkart-style: automatic refund)
            if (order.PaymentStatus == "Paid")
            {
                try
                {
                    var refundTransactionId = GenerateRefundTransactionId();

                    order.RefundStatus = "Initiated";
                    order.RefundAmount = order.Total;
                    order.RefundTransactionId = refundTransactionId;
                    order.RefundDate = DateTime.UtcNow;

                    // Simulate refund processing (in production, this would be async via webhook)
                    // Flipkart typically processes refunds within 5-7 business days
                    order.RefundStatus = "Processing";
                }
                catch (Exception refundEx)
                {
                    _logger?.LogError(refundEx, "Error processing refund for cancelled order {OrderId}", order.Id);
                    order.RefundStatus = "Initiated";
                    order.RefundAmount = order.Total;
                    order.RefundDate = DateTime.UtcNow;
                }
            }
            else if (order.PaymentMethod == "CashOnDelivery" || string.IsNullOrEmpty(order.PaymentStatus))
            {
                // No refund needed for COD
                order.RefundStatus = "Not Applicable";
            }

            await _context.SaveChangesAsync();

            if (order.PaymentStatus == "Paid")
            {
                TempData["Success"] = "Order cancelled successfully. Refund of ₹" + order.Total.ToString("F2") + 
                    " will be processed to your original payment method within 5-7 business days.";
            }
            else
            {
                TempData["Success"] = "Order cancelled successfully.";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error cancelling order {OrderId}", id);
            TempData["Error"] = "An error occurred while cancelling the order. Please try again.";
        }
        
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnOrder(int id, string? reason, string? returnType)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userId))
        {
            TempData["Error"] = "Unable to identify user. Please log in again.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction(nameof(Index));
            }

            // Only allow return if order is delivered (Flipkart allows returns within 7-10 days of delivery)
            if (order.Status != "Delivered" && order.Status != "Confirmed")
            {
                TempData["Error"] = "Only delivered or confirmed orders can be returned.";
                return RedirectToAction(nameof(Index));
            }

            // Check if already returned
            if (order.Status == "Return Requested" || order.Status == "Returned")
            {
                TempData["Info"] = "Return request already exists for this order.";
                return RedirectToAction(nameof(Index));
            }

            // Check if return is within allowed period (7 days from order date)
            var orderDate = order.CreatedAt != default(DateTime) ? order.CreatedAt : DateTime.UtcNow;
            var daysSinceOrder = (DateTime.UtcNow - orderDate).TotalDays;
            if (daysSinceOrder > 7)
            {
                TempData["Error"] = "Return period has expired. Returns are allowed within 7 days of delivery.";
                return RedirectToAction(nameof(Index));
            }

            // Update order status
            order.Status = "Return Requested";

            // Restore stock for all items
            if (order.Items != null)
            {
                foreach (var item in order.Items)
                {
                    if (item.ProductId > 0)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.Stock += item.Quantity;
                        }
                    }
                }
            }

            // Process refund (Flipkart-style: automatic refund on return approval)
            if (order.PaymentStatus == "Paid")
            {
                try
                {
                    var refundTransactionId = GenerateRefundTransactionId();

                    order.RefundStatus = "Initiated";
                    order.RefundAmount = order.Total;
                    order.RefundTransactionId = refundTransactionId;
                    order.RefundDate = DateTime.UtcNow;

                    // Simulate refund processing
                    order.RefundStatus = "Processing";
                }
                catch (Exception refundEx)
                {
                    _logger?.LogError(refundEx, "Error processing refund for order {OrderId}", order.Id);
                    order.RefundStatus = "Initiated";
                    order.RefundAmount = order.Total;
                    order.RefundDate = DateTime.UtcNow;
                }
            }
            else if (order.PaymentMethod == "CashOnDelivery")
            {
                order.RefundStatus = "Not Applicable";
            }

            await _context.SaveChangesAsync();

            if (order.PaymentStatus == "Paid")
            {
                TempData["Success"] = "Return request submitted successfully. Refund of ₹" + order.Total.ToString("F2") + 
                    " will be processed to your original payment method within 5-7 business days after we receive the product.";
            }
            else
            {
                TempData["Success"] = "Return request submitted successfully. We will arrange pickup of the product.";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing return for order {OrderId}", id);
            TempData["Error"] = "An error occurred while processing your return request. Please try again.";
        }
        
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> RefundForm(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userId))
        {
            TempData["Error"] = "Unable to identify user. Please log in again.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction(nameof(Index));
            }

            // Ensure Items list is initialized
            if (order.Items == null)
            {
                order.Items = new List<OrderItem>();
            }

            // Only allow refund requests for paid orders
            if (order.PaymentStatus != "Paid")
            {
                TempData["Error"] = "Refund can only be requested for paid orders.";
                return RedirectToAction(nameof(Index));
            }

            // Check if refund already exists
            if (!string.IsNullOrEmpty(order.RefundStatus) && order.RefundStatus != "Not Applicable")
            {
                TempData["Info"] = "Refund request already exists for this order.";
                return RedirectToAction(nameof(Index));
            }

            // Ensure required fields have defaults
            if (order.CreatedAt == default(DateTime)) order.CreatedAt = DateTime.UtcNow;
            if (string.IsNullOrEmpty(order.PaymentMethod)) order.PaymentMethod = "CashOnDelivery";

            return View(order);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading refund form for order {OrderId}", id);
            TempData["Error"] = "An error occurred while loading the refund form. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitRefundRequest(int id, string reason, string? additionalDetails)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userId))
        {
            TempData["Error"] = "Unable to identify user. Please log in again.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrEmpty(reason))
        {
            TempData["Error"] = "Please provide a reason for the refund request.";
            return RedirectToAction(nameof(RefundForm), new { id });
        }

        try
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction(nameof(Index));
            }

            // Only allow refund requests for paid orders
            if (order.PaymentStatus != "Paid")
            {
                TempData["Error"] = "Refund can only be requested for paid orders.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrEmpty(order.RefundStatus) || order.RefundStatus == "Not Applicable")
            {
                try
                {
                    var refundTransactionId = GenerateRefundTransactionId();

                    order.RefundStatus = "Initiated";
                    order.RefundAmount = order.Total;
                    order.RefundTransactionId = refundTransactionId;
                    order.RefundDate = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Refund request submitted successfully. Reason: {reason}. Refund of ₹" + order.Total.ToString("F2") +
                        " will be processed to your original payment method within 5-7 business days after we receive the returned product.";
                }
                catch (Exception refundEx)
                {
                    _logger?.LogError(refundEx, "Error processing refund for order {OrderId}", order.Id);
                    order.RefundStatus = "Initiated";
                    order.RefundAmount = order.Total;
                    order.RefundDate = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Refund request submitted successfully. Reason: {reason}. Refund of ₹" + order.Total.ToString("F2") +
                        " will be processed to your original payment method within 5-7 business days.";
                }
            }
            else
            {
                TempData["Info"] = "Refund request already exists for this order.";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error submitting refund request for order {OrderId}", id);
            TempData["Error"] = "An error occurred while submitting your refund request. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestRefund(int id, string? reason)
    {
        // Redirect to refund form for better UX
        return RedirectToAction(nameof(RefundForm), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnItem(int orderId, int itemId, string? reason)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userId))
        {
            TempData["Error"] = "Unable to identify user. Please log in again.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction(nameof(Index));
            }

            // Ensure Items list is initialized
            if (order.Items == null || !order.Items.Any())
            {
                TempData["Error"] = "Order items not found.";
                return RedirectToAction(nameof(Index));
            }

            var orderItem = order.Items.FirstOrDefault(i => i.Id == itemId);
            if (orderItem == null)
            {
                TempData["Error"] = "Order item not found.";
                return RedirectToAction(nameof(Index));
            }

            // Only allow return if order is delivered or confirmed
            if (order.Status != "Delivered" && order.Status != "Confirmed")
            {
                TempData["Error"] = "Only delivered or confirmed orders can have items returned.";
                return RedirectToAction(nameof(Index));
            }

            // Check if item is already returned
            if (orderItem.ReturnStatus == "Returned" || orderItem.ReturnStatus == "Return Requested")
            {
                TempData["Info"] = "This item has already been returned or return request is pending.";
                return RedirectToAction(nameof(Index));
            }

            // Check if return is within allowed period (7 days from order)
            var orderDate = order.CreatedAt != default(DateTime) ? order.CreatedAt : DateTime.UtcNow;
            var daysSinceOrder = (DateTime.UtcNow - orderDate).TotalDays;
            if (daysSinceOrder > 7)
            {
                TempData["Error"] = "Return period has expired. Returns are allowed within 7 days of delivery.";
                return RedirectToAction(nameof(Index));
            }

            // Update item return status
            orderItem.ReturnStatus = "Return Requested";
            orderItem.ReturnRequestDate = DateTime.UtcNow;
            var itemTotal = orderItem.UnitPrice * orderItem.Quantity;
            orderItem.RefundAmount = itemTotal;

            // Restore stock for returned item
            if (orderItem.ProductId > 0)
            {
                var product = await _context.Products.FindAsync(orderItem.ProductId);
                if (product != null)
                {
                    product.Stock += orderItem.Quantity;
                }
            }

            // Process partial refund if payment was made
            if (order.PaymentStatus == "Paid")
            {
                try
                {
                    var refundTransactionId = GenerateRefundTransactionId();

                    // Update order refund if this is the first return
                    if (string.IsNullOrEmpty(order.RefundStatus) || order.RefundStatus == "Not Applicable")
                    {
                        order.RefundStatus = "Processing";
                        order.RefundAmount = itemTotal;
                        order.RefundTransactionId = refundTransactionId;
                        order.RefundDate = DateTime.UtcNow;
                    }
                    else
                    {
                        // Add to existing refund amount
                        order.RefundAmount = (order.RefundAmount ?? 0) + itemTotal;
                    }
                }
                catch (Exception refundEx)
                {
                    _logger?.LogError(refundEx, "Error processing refund for item {ItemId} in order {OrderId}", itemId, orderId);
                    // Still update refund status even if payment service fails
                    if (string.IsNullOrEmpty(order.RefundStatus) || order.RefundStatus == "Not Applicable")
                    {
                        order.RefundStatus = "Processing";
                        order.RefundAmount = itemTotal;
                        order.RefundDate = DateTime.UtcNow;
                    }
                    else
                    {
                        order.RefundAmount = (order.RefundAmount ?? 0) + itemTotal;
                    }
                }
            }

            await _context.SaveChangesAsync();

            var productName = orderItem.Product?.Name ?? "Item";
            TempData["Success"] = $"Return request submitted for {productName}. Refund of ₹{itemTotal:F2} " +
                "will be processed to your original payment method within 5-7 business days after we receive the product.";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing return for item {ItemId} in order {OrderId}", itemId, orderId);
            TempData["Error"] = "An error occurred while processing your return request. Please try again.";
        }
        
        return RedirectToAction(nameof(Index));
    }

    private static string GetPaymentReference(Order order)
    {
        if (!string.IsNullOrWhiteSpace(order.RazorpayPaymentId))
        {
            return order.RazorpayPaymentId;
        }

        if (!string.IsNullOrWhiteSpace(order.RazorpayOrderId))
        {
            return order.RazorpayOrderId;
        }

        // Fallback to a simple reference based on order id
        return $"ORDER-{order.Id}";
    }
}
