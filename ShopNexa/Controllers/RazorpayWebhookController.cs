using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopNexa.Data;
using ShopNexa.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ShopNexa.Controllers;

/// <summary>
/// Handles Razorpay webhook events for payment confirmations.
/// This provides a reliable way to confirm payments even if the user's browser
/// disconnects during the payment flow.
/// </summary>
[ApiController]
[Route("api/webhooks/razorpay")]
public class RazorpayWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly RazorpayService _razorpayService;
    private readonly ILogger<RazorpayWebhookController> _logger;
    private readonly IConfiguration _configuration;

    public RazorpayWebhookController(
        ApplicationDbContext context,
        RazorpayService razorpayService,
        ILogger<RazorpayWebhookController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _razorpayService = razorpayService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Receives and processes Razorpay webhook events.
    /// Validates webhook signature to ensure request is from Razorpay.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        try
        {
            // Read the request body
            using var reader = new StreamReader(Request.Body);
            var webhookBody = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(webhookBody))
            {
                _logger.LogWarning("Received empty webhook body");
                return BadRequest("Empty request body");
            }

            // Get the webhook signature from headers
            var webhookSignature = Request.Headers["X-Razorpay-Signature"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(webhookSignature))
            {
                _logger.LogWarning("Missing X-Razorpay-Signature header");
                return BadRequest("Missing signature header");
            }

            // Verify webhook signature
            var webhookSecret = _configuration["Razorpay:WebhookSecret"];
            if (string.IsNullOrWhiteSpace(webhookSecret))
            {
                _logger.LogError("Webhook secret is not configured");
                return StatusCode(500, "Webhook secret not configured");
            }

            if (!VerifyWebhookSignature(webhookBody, webhookSignature, webhookSecret))
            {
                _logger.LogWarning("Invalid webhook signature");
                return BadRequest("Invalid signature");
            }

            // Parse the webhook payload
            var webhookEvent = JsonSerializer.Deserialize<JsonElement>(webhookBody);
            var eventType = webhookEvent.GetProperty("event").GetString();

            _logger.LogInformation("Received Razorpay webhook event: {EventType}", eventType);

            // Process based on event type
            switch (eventType)
            {
                case "payment.captured":
                    await HandlePaymentCaptured(webhookEvent);
                    break;

                case "payment.failed":
                    await HandlePaymentFailed(webhookEvent);
                    break;

                case "order.paid":
                    await HandleOrderPaid(webhookEvent);
                    break;

                default:
                    _logger.LogInformation("Unhandled webhook event type: {EventType}", eventType);
                    break;
            }

            // Always return 200 OK to Razorpay to prevent retries
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Razorpay webhook");
            // Return 200 to prevent Razorpay from retrying
            // Log the error for manual investigation
            return Ok();
        }
    }

    /// <summary>
    /// Handles payment.captured event - payment was successfully captured.
    /// </summary>
    private async Task HandlePaymentCaptured(JsonElement webhookEvent)
    {
        try
        {
            var payload = webhookEvent.GetProperty("payload").GetProperty("payment").GetProperty("entity");
            var paymentId = payload.GetProperty("id").GetString();
            var orderId = payload.GetProperty("order_id").GetString();
            var amount = payload.GetProperty("amount").GetInt32() / 100.0m; // Convert from paise
            var status = payload.GetProperty("status").GetString();

            _logger.LogInformation("Processing payment.captured: Payment={PaymentId}, Order={OrderId}, Status={Status}",
                paymentId, orderId, status);

            if (string.IsNullOrEmpty(orderId))
            {
                _logger.LogWarning("Payment captured event missing order_id");
                return;
            }

            // Find the order by Razorpay order ID
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.RazorpayOrderId == orderId);

            if (order == null)
            {
                _logger.LogWarning("Order not found for Razorpay order ID: {OrderId}", orderId);
                return;
            }

            // Idempotency check
            if (order.PaymentStatus == "Paid")
            {
                _logger.LogInformation("Order {OrderId} is already marked as paid", order.Id);
                return;
            }

            // Use transaction for atomic update
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Update order status
                order.PaymentStatus = "Paid";
                order.Status = "Confirmed";
                order.RazorpayPaymentId = paymentId;
                order.PaymentDate = DateTime.UtcNow;

                // Reduce stock
                foreach (var item in order.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.Stock = Math.Max(0, product.Stock - item.Quantity);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Order {OrderId} marked as paid via webhook", order.Id);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating order {OrderId} via webhook", order.Id);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling payment.captured webhook");
            throw;
        }
    }

    /// <summary>
    /// Handles payment.failed event - payment attempt failed.
    /// </summary>
    private async Task HandlePaymentFailed(JsonElement webhookEvent)
    {
        try
        {
            var payload = webhookEvent.GetProperty("payload").GetProperty("payment").GetProperty("entity");
            var paymentId = payload.GetProperty("id").GetString();
            var orderId = payload.GetProperty("order_id").GetString();

            _logger.LogWarning("Payment failed: Payment={PaymentId}, Order={OrderId}", paymentId, orderId);

            if (string.IsNullOrEmpty(orderId))
                return;

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.RazorpayOrderId == orderId);

            if (order == null || order.PaymentStatus == "Paid")
                return;

            order.PaymentStatus = "Failed";
            order.Status = "Payment Failed";
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling payment.failed webhook");
        }
    }

    /// <summary>
    /// Handles order.paid event - entire order is paid.
    /// </summary>
    private async Task HandleOrderPaid(JsonElement webhookEvent)
    {
        // Similar to payment.captured but for order-level events
        // This is typically redundant with payment.captured for single payments
        _logger.LogInformation("Received order.paid webhook event");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies the webhook signature using HMAC SHA256.
    /// </summary>
    private bool VerifyWebhookSignature(string webhookBody, string signature, string secret)
    {
        try
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(webhookBody));
            var expectedSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(signature)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying webhook signature");
            return false;
        }
    }
}
