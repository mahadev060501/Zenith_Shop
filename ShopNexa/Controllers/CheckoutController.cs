using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopNexa.Data;
using ShopNexa.Models;
using ShopNexa.Services;

namespace ShopNexa.Controllers;

[Authorize]
public class CheckoutController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CartService _cartService;
    private readonly RazorpayService _razorpayService;
    private readonly EmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ApplicationDbContext context,
        CartService cartService,
        RazorpayService razorpayService,
        EmailService emailService,
        IConfiguration configuration,
        ILogger<CheckoutController> logger)
    {
        _context = context;
        _cartService = cartService;
        _razorpayService = razorpayService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    private decimal CalculateShipping(decimal subtotal)
    {
        // Flat ₹49 shipping for orders below ₹500, otherwise free
        return subtotal < 500m && subtotal > 0m ? 49m : 0m;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cart = await _cartService.GetCartAsync();
        if (!cart.Any()) return RedirectToAction("Index", "Cart");

        var subtotal = await _cartService.GetCartTotalAsync();
        var shipping = CalculateShipping(subtotal);

        ViewBag.Subtotal = subtotal;
        ViewBag.Shipping = shipping;
        ViewBag.Total = subtotal + shipping;

        // Prefill shipping details from user's most recent order (Flipkart/Amazon-style)
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Order model = new();

        if (!string.IsNullOrEmpty(userId))
        {
            var lastOrder = await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastOrder != null)
            {
                model.CustomerName = lastOrder.CustomerName;
                model.Email = lastOrder.Email;
                model.AddressLine1 = lastOrder.AddressLine1;
                model.AddressLine2 = lastOrder.AddressLine2;
                model.City = lastOrder.City;
                model.Country = string.IsNullOrEmpty(lastOrder.Country) ? "India" : lastOrder.Country;
                model.PostalCode = lastOrder.PostalCode;
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(Order order)
    {
        var cart = await _cartService.GetCartAsync();
        if (!cart.Any())
        {
            ModelState.AddModelError("", "Your cart is empty.");
            var emptySubtotal = await _cartService.GetCartTotalAsync();
            ViewBag.Subtotal = emptySubtotal;
            ViewBag.Shipping = CalculateShipping(emptySubtotal);
            ViewBag.Total = emptySubtotal + ViewBag.Shipping;
            return View(order);
        }

        if (!ModelState.IsValid)
        {
            var invalidSubtotal = await _cartService.GetCartTotalAsync();
            ViewBag.Subtotal = invalidSubtotal;
            ViewBag.Shipping = CalculateShipping(invalidSubtotal);
            ViewBag.Total = invalidSubtotal + ViewBag.Shipping;
            return View(order);
        }

        // Validate stock availability
        foreach (var cartItem in cart)
        {
            var product = await _context.Products.FindAsync(cartItem.ProductId);
            if (product == null || product.Stock < cartItem.Quantity)
            {
                ModelState.AddModelError("", $"{cartItem.Name} is out of stock or insufficient quantity available.");
                ViewBag.Total = await _cartService.GetCartTotalAsync();
                return View(order);
            }
        }

        // Store order details in TempData for payment page
        TempData["Order_CustomerName"] = order.CustomerName;
        TempData["Order_Email"] = order.Email;
        TempData["Order_AddressLine1"] = order.AddressLine1;
        TempData["Order_AddressLine2"] = order.AddressLine2 ?? "";
        TempData["Order_City"] = order.City;
        TempData["Order_Country"] = order.Country;
        TempData["Order_PostalCode"] = order.PostalCode ?? "";

        var subtotal = await _cartService.GetCartTotalAsync();
        var shipping = CalculateShipping(subtotal);
        var total = subtotal + shipping;

        TempData["Order_Total"] = total.ToString();

        // Redirect to payment selection page
        return RedirectToAction("Payment");
    }

    [HttpGet]
    public async Task<IActionResult> Payment()
    {
        var totalString = TempData["Order_Total"] as string;
        if (string.IsNullOrEmpty(totalString))
        {
            TempData["Error"] = "Please complete shipping details first.";
            return RedirectToAction("Index");
        }

        // Keep TempData for next request
        TempData.Keep();

        if (!decimal.TryParse(totalString, out var total))
        {
            TempData["Error"] = "There was a problem with your order total. Please try again.";
            return RedirectToAction("Index");
        }

        // For display we split total into subtotal + shipping again based on cart
        var subtotal = await _cartService.GetCartTotalAsync();
        var shipping = CalculateShipping(subtotal);

        ViewBag.Subtotal = subtotal;
        ViewBag.Shipping = shipping;
        ViewBag.Total = total;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Payment(string paymentMethod)
    {
        var cart = await _cartService.GetCartAsync();
        if (!cart.Any())
        {
            TempData["Error"] = "Your cart is empty.";
            return RedirectToAction("Index", "Cart");
        }

        if (string.IsNullOrEmpty(paymentMethod))
        {
            ModelState.AddModelError("PaymentMethod", "Please select a payment method.");
            var subtotalMissingPayment = await _cartService.GetCartTotalAsync();
            ViewBag.Subtotal = subtotalMissingPayment;
            ViewBag.Shipping = CalculateShipping(subtotalMissingPayment);
            ViewBag.Total = subtotalMissingPayment + ViewBag.Shipping;
            return View();
        }

        // Get order details from TempData
        var customerName = TempData["Order_CustomerName"] as string;
        var email = TempData["Order_Email"] as string;
        var addressLine1 = TempData["Order_AddressLine1"] as string;
        var addressLine2 = TempData["Order_AddressLine2"] as string;
        var city = TempData["Order_City"] as string;
        var country = TempData["Order_Country"] as string;
        var postalCode = TempData["Order_PostalCode"] as string;
        var totalString = TempData["Order_Total"] as string;

        if (string.IsNullOrEmpty(customerName) || string.IsNullOrEmpty(email))
        {
            TempData["Error"] = "Please complete shipping details first.";
            return RedirectToAction("Index");
        }

        if (!decimal.TryParse(totalString, out var total))
        {
            TempData["Error"] = "Invalid order total.";
            return RedirectToAction("Index");
        }

        // Validate stock availability again
        foreach (var cartItem in cart)
        {
            var product = await _context.Products.FindAsync(cartItem.ProductId);
            if (product == null || product.Stock < cartItem.Quantity)
            {
                TempData["Error"] = $"{cartItem.Name} is out of stock or insufficient quantity available.";
                return RedirectToAction("Index", "Cart");
            }
        }

        // Create order with pending payment status, including shipping charges if applicable
        var subtotal = cart.Sum(c => c.Price * c.Quantity);
        var shipping = CalculateShipping(subtotal);

        var order = new Order
        {
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            Total = subtotal + shipping,
            CreatedAt = DateTime.UtcNow,
            Status = "Pending",
            PaymentMethod = paymentMethod,
            PaymentStatus = "Pending",
            CustomerName = customerName ?? "",
            Email = email ?? "",
            AddressLine1 = addressLine1 ?? "",
            AddressLine2 = addressLine2 ?? "",
            City = city ?? "",
            Country = country ?? "",
            PostalCode = postalCode ?? "",
            Items = cart.Select(c => new OrderItem
            {
                ProductId = c.ProductId,
                Quantity = c.Quantity,
                UnitPrice = c.Price
            }).ToList()
        };

        // If Cash on Delivery, confirm order immediately
        if (paymentMethod == "COD")
        {
            // Reduce stock
            foreach (var item in order.Items)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.Stock = Math.Max(0, product.Stock - item.Quantity);
                }
            }

            order.Status = "Confirmed";
            order.PaymentStatus = "Pending";
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Send email notifications
            await SendOrderEmailsAsync(order);

            await _cartService.ClearCartAsync();
            TempData["Success"] = "Order placed successfully! Payment on delivery.";
            return RedirectToAction("Success", "Orders", new { id = order.Id });
        }

        // For Razorpay (online), order is pending until payment is confirmed
        order.Status = "Pending";
        order.PaymentStatus = "Pending";
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Store order ID in TempData for verification
        TempData["PendingOrderId"] = order.Id.ToString();
        TempData.Keep();

        // Redirect to payment page where Razorpay Checkout will be launched
        return RedirectToAction("ProcessPayment", new { id = order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> ProcessPayment(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        // Verify user owns this order
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (order.UserId != userId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        // Check if already paid
        if (order.PaymentStatus == "Paid")
        {
            TempData["Info"] = "This order has already been paid.";
            return RedirectToAction("Success", "Orders", new { id = order.Id });
        }

        // For Razorpay integration, create Razorpay order if this is an online payment
        if (order.PaymentMethod == "Razorpay")
        {
            // Check if Razorpay is configured
            if (!_razorpayService.IsConfigured)
            {
                TempData["Error"] = "Online payment is not available at the moment. Please try Cash on Delivery or contact support.";
                return RedirectToAction("Index", "Cart");
            }

            try
            {
                // If we already have a Razorpay order ID and order is still pending, reuse it
                if (string.IsNullOrEmpty(order.RazorpayOrderId))
                {
                    var razorpayOrderId = await _razorpayService.CreateRazorpayOrderAsync(order.Total, order.Id.ToString());
                    order.RazorpayOrderId = razorpayOrderId;
                    await _context.SaveChangesAsync();
                }

                ViewBag.RazorpayKeyId = _razorpayService.GetKeyId();
                ViewBag.RazorpayOrderId = order.RazorpayOrderId;
                ViewBag.RazorpayAmount = (int)(order.Total * 100); // Amount in paise
                ViewBag.CustomerName = order.CustomerName;
                ViewBag.CustomerEmail = order.Email;
                ViewBag.CustomerContact = order.PostalCode ?? "9999999999";
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not configured"))
            {
                _logger.LogError(ex, "Razorpay is not properly configured");
                TempData["Error"] = "Online payment is not configured. Please use Cash on Delivery or contact support.";
                return RedirectToAction("Index", "Cart");
            }
            catch (Exception ex) when (ex.Message.Contains("Authentication failed") || ex.InnerException?.Message.Contains("Authentication failed") == true)
            {
                _logger.LogError(ex, "Razorpay authentication failed - invalid credentials");
                TempData["Error"] = "Payment gateway authentication failed. Please use Cash on Delivery or contact support.";
                return RedirectToAction("Index", "Cart");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Razorpay payment");
                TempData["Error"] = "Unable to initialize payment gateway. Please try again or contact support.";
                return RedirectToAction("Index", "Cart");
            }
        }

        return View(order);
    }

    /// <summary>
    /// Called from Razorpay Checkout JS after payment success.
    /// Verifies signature (HMAC SHA256), updates order and clears cart, then redirects to success page.
    /// This action is idempotent - calling it multiple times for the same payment will not cause duplicate processing.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyPayment(string razorpay_payment_id, string razorpay_order_id, string razorpay_signature)
    {
        // Validate input parameters
        if (string.IsNullOrWhiteSpace(razorpay_payment_id) ||
            string.IsNullOrWhiteSpace(razorpay_order_id) ||
            string.IsNullOrWhiteSpace(razorpay_signature))
        {
            TempData["Error"] = "Invalid payment details received.";
            return RedirectToAction("Index", "Cart");
        }

        // Find order by Razorpay order ID
        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.RazorpayOrderId == razorpay_order_id);

        if (order == null)
        {
            TempData["Error"] = "Order not found for this payment.";
            return RedirectToAction("Index", "Cart");
        }

        // Verify user owns this order (security check)
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (order.UserId != userId && !User.IsInRole("Admin"))
        {
            TempData["Error"] = "Unauthorized access to this order.";
            return RedirectToAction("Index", "Cart");
        }

        // Idempotency check: If already paid, redirect to success without reprocessing
        if (order.PaymentStatus == "Paid")
        {
            return RedirectToAction("Success", "Orders", new { id = order.Id });
        }

        // Verify Razorpay signature using HMAC SHA256
        if (!_razorpayService.VerifySignature(razorpay_order_id, razorpay_payment_id, razorpay_signature))
        {
            order.PaymentStatus = "Failed";
            order.Status = "Payment Failed";
            await _context.SaveChangesAsync();

            TempData["Error"] = "Payment verification failed. Please contact support if amount was deducted.";
            return RedirectToAction("Index", "Cart");
        }

        // Optional: Verify payment details with Razorpay API for additional security
        var paymentDetails = await _razorpayService.FetchPaymentDetailsAsync(razorpay_payment_id);
        if (paymentDetails != null)
        {
            // Verify the payment amount matches the order amount
            if (paymentDetails.Amount != order.Total)
            {
                _logger.LogWarning("Payment amount mismatch for order {OrderId}. Expected: {Expected}, Received: {Received}",
                    order.Id, order.Total, paymentDetails.Amount);
                // Log for manual review but don't fail - Razorpay signature is already verified
            }

            // Verify payment status is captured
            if (paymentDetails.Status != "captured")
            {
                order.PaymentStatus = "Pending";
                order.Status = "Payment Pending";
                await _context.SaveChangesAsync();

                TempData["Warning"] = "Payment is being processed. Please check your order status shortly.";
                return RedirectToAction("Index", "Orders");
            }
        }

        // Use transaction to ensure atomicity
        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Validate stock availability
            foreach (var item in order.Items)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null || product.Stock < item.Quantity)
                {
                    await dbTransaction.RollbackAsync();
                    order.PaymentStatus = "Failed";
                    order.Status = "Failed - Stock Unavailable";
                    await _context.SaveChangesAsync();

                    TempData["Error"] = $"{item.Product?.Name ?? "Item"} is out of stock. Refund will be processed if payment was made.";
                    return RedirectToAction("Index", "Cart");
                }
            }

            // Reduce stock
            foreach (var item in order.Items)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.Stock = Math.Max(0, product.Stock - item.Quantity);
                }
            }

            // Update order with payment confirmation
            order.PaymentMethod = "Razorpay";
            order.PaymentStatus = "Paid";
            order.Status = "Confirmed";
            order.RazorpayPaymentId = razorpay_payment_id;
            order.RazorpaySignature = razorpay_signature;
            order.PaymentDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Payment verified successfully for order {OrderId}, payment {PaymentId}",
                order.Id, razorpay_payment_id);

            // Send email notifications
            await SendOrderEmailsAsync(order);
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Error processing payment verification for order {OrderId}", order.Id);

            TempData["Error"] = "An error occurred while processing your payment. Please contact support.";
            return RedirectToAction("Index", "Cart");
        }

        // Clear cart after successful payment
        await _cartService.ClearCartAsync();

        // Redirect to order success page
        return RedirectToAction("Success", "Orders", new { id = order.Id });
    }

    public async Task<IActionResult> Confirmation(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (order.UserId != userId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        return View(order);
    }

    private async Task SendOrderEmailsAsync(Order order)
    {
        try
        {
            var orderForEmail = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == order.Id);

            if (orderForEmail == null)
            {
                return;
            }

            var adminEmail = _configuration["SmtpSettings:AdminEmail"] ?? _configuration["Email:AdminEmail"];

            // 1) User email
            await _emailService.SendOrderConfirmationEmailAsync(orderForEmail.Email, orderForEmail.CustomerName, orderForEmail);

            // Seller email (only if not admin product)
            var sellerItem = orderForEmail.Items.FirstOrDefault(i => i.Product?.SellerId != null);
            var sellerId = sellerItem?.Product?.SellerId;
            var sellerUser = !string.IsNullOrEmpty(sellerId)
                ? await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == sellerId)
                : null;
            var sellerProfile = !string.IsNullOrEmpty(sellerId)
                ? await _context.SellerProfiles.AsNoTracking().FirstOrDefaultAsync(sp => sp.UserId == sellerId)
                : null;

            var sellerEmail = sellerUser?.Email;
            var sellerName = sellerProfile?.CompanyName ?? sellerUser?.FullName ?? sellerUser?.Email ?? "Seller";

            if (!string.IsNullOrEmpty(sellerEmail) &&
                !string.Equals(sellerEmail, adminEmail, StringComparison.OrdinalIgnoreCase))
            {
                var sellerHtmlBody = BuildSellerNotificationBody(orderForEmail, sellerName);
                await _emailService.SendEmailAsync(
                    sellerEmail,
                    "New Order Received - Zenith Shop",
                    sellerHtmlBody
                );
            }

            // Admin email (always send)
            if (!string.IsNullOrWhiteSpace(adminEmail))
            {
                var adminHtmlBody = BuildAdminAlertBody(orderForEmail, sellerName);
                await _emailService.SendEmailAsync(
                    adminEmail,
                    "New Order Alert - Zenith Shop",
                    adminHtmlBody
                );
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Email error: " + ex.Message);
            _logger.LogWarning(ex, "Email sending failed for order {OrderId}", order.Id);
        }
    }

    private string BuildAdminAlertBody(Order order, string sellerName)
    {
        var firstItem = order.Items.FirstOrDefault();
        var productName = firstItem?.Product?.Name ?? "Product";
        var quantity = order.Items.Sum(i => i.Quantity);
        var totalAmount = order.Total.ToString("N2");
        var customerName = order.CustomerName;
        var customerEmail = order.Email;

        return $@"
<h2>New Order Alert</h2>
<hr/>
<p><strong>Order ID:</strong> {order.Id}</p>
<p><strong>Customer Name:</strong> {customerName}</p>
<p><strong>Customer Email:</strong> {customerEmail}</p>
<p><strong>Product:</strong> {productName}</p>
<p><strong>Seller:</strong> {sellerName}</p>
<p><strong>Quantity:</strong> {quantity}</p>
<p><strong>Total Amount:</strong> ₹{totalAmount}</p>
<p><strong>Date:</strong> {DateTime.Now}</p>
<hr/>
<p>Please check the Admin Dashboard for more details.</p>
";
    }

    private string BuildSellerNotificationBody(Order order, string sellerName)
    {
        var itemsHtml = string.Join("", order.Items.Select(item => $@"
<tr>
    <td style='padding: 8px 0;'>{item.Product?.Name}</td>
    <td style='padding: 8px 0; text-align: center;'>{item.Quantity}</td>
    <td style='padding: 8px 0; text-align: right;'>₹{(item.UnitPrice * item.Quantity):N2}</td>
</tr>
"));

        return $@"
<h2>New Order Received</h2>
<hr/>
<p><strong>Seller:</strong> {sellerName}</p>
<p><strong>Order ID:</strong> {order.Id}</p>
<p><strong>Customer:</strong> {order.CustomerName}</p>
<p><strong>Email:</strong> {order.Email}</p>
<table style='width:100%; border-collapse: collapse;'>
    <thead>
        <tr style='border-bottom:1px solid #eee;'>
            <th style='text-align:left;'>Product</th>
            <th style='text-align:center;'>Qty</th>
            <th style='text-align:right;'>Total</th>
        </tr>
    </thead>
    <tbody>
        {itemsHtml}
    </tbody>
</table>
<p style='margin-top:12px;'><strong>Order Total:</strong> ₹{order.Total:N2}</p>
<hr/>
<p>Please fulfill this order from your seller dashboard.</p>
";
    }
}
