using System.Security.Cryptography;
using System.Text;
using Razorpay.Api;
using Microsoft.Extensions.Logging;

namespace ShopNexa.Services;

public class RazorpayService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RazorpayService> _logger;
    private readonly string _keyId;
    private readonly string _keySecret;

    public RazorpayService(IConfiguration configuration, ILogger<RazorpayService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        // Check environment variables first, then fall back to configuration
        _keyId = Environment.GetEnvironmentVariable("RAZORPAY_KEY_ID") 
            ?? _configuration["Razorpay:KeyId"] 
            ?? "";
        _keySecret = Environment.GetEnvironmentVariable("RAZORPAY_KEY_SECRET") 
            ?? _configuration["Razorpay:KeySecret"] 
            ?? "";
    }

    public string GetKeyId() => _keyId;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_keyId) && !string.IsNullOrWhiteSpace(_keySecret);

    /// <summary>
    /// Creates a Razorpay order using official SDK and returns the Razorpay Order Id.
    /// Throws exception if Razorpay is not configured or if order creation fails.
    /// </summary>
    public async Task<string> CreateRazorpayOrderAsync(decimal amount, string receipt)
    {
        if (!IsConfigured)
        {
            _logger.LogError("Razorpay is not configured. KeyId or KeySecret is missing.");
            throw new InvalidOperationException("Razorpay payment gateway is not configured. Please contact support.");
        }

        try
        {
            // Run Razorpay SDK call on a background thread since it's synchronous
            return await Task.Run(() =>
            {
                var client = new RazorpayClient(_keyId, _keySecret);

                var options = new Dictionary<string, object>
                {
                    { "amount", (int)(amount * 100) }, // convert to paise
                    { "currency", "INR" },
                    { "receipt", receipt },
                    { "payment_capture", 1 } // Auto-capture payment
                };

                var receiptStr = receipt.ToString();
                var amountStr = amount.ToString("F2");
                _logger.LogInformation("Creating Razorpay order for receipt: {Receipt}, amount: {Amount}", receiptStr, amountStr);

                var order = client.Order.Create(options);
                string razorpayOrderId = Convert.ToString(order["id"]);

                if (string.IsNullOrEmpty(razorpayOrderId))
                {
                    throw new InvalidOperationException("Razorpay order creation returned empty order ID");
                }

                _logger.LogInformation("Razorpay order created successfully: {OrderId}", (string)razorpayOrderId);

                return razorpayOrderId;
            });
        }
        catch (InvalidOperationException)
        {
            // Re-throw our own exceptions
            throw;
        }
        catch (Exception ex) when (ex.Message.Contains("Authentication failed") || ex.Message.Contains("BadRequestError"))
        {
            _logger.LogError(ex, "Razorpay authentication failed. Check KeyId and KeySecret configuration.");
            throw new InvalidOperationException("Payment gateway authentication failed. Please check your Razorpay credentials.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Razorpay order for receipt: {Receipt}", receipt);
            throw new InvalidOperationException("Failed to create payment order. Please try again or contact support.", ex);
        }
    }

    /// <summary>
    /// Generates HMAC SHA256 signature for Razorpay payment verification.
    /// </summary>
    public string GenerateSignature(string orderId, string paymentId)
    {
        if (string.IsNullOrEmpty(_keySecret))
        {
            _logger.LogError("Cannot generate signature: KeySecret is not configured");
            throw new InvalidOperationException("Razorpay key secret is not configured");
        }

        var payload = $"{orderId}|{paymentId}";
        var keyBytes = Encoding.UTF8.GetBytes(_keySecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }

    /// <summary>
    /// Verifies Razorpay payment signature using HMAC SHA256.
    /// Returns true only if signature is valid and keys are configured.
    /// </summary>
    public bool VerifySignature(string orderId, string paymentId, string signature)
    {
        if (string.IsNullOrEmpty(_keySecret))
        {
            _logger.LogError("Cannot verify signature: KeySecret is not configured");
            return false;
        }

        if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("Signature verification failed: Missing required parameters");
            return false;
        }

        try
        {
            var expectedSignature = GenerateSignature(orderId, paymentId);
            var isValid = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(signature)
            );

            if (!isValid)
            {
                _logger.LogWarning("Signature verification failed for order: {OrderId}, payment: {PaymentId}", orderId, paymentId);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during signature verification for order: {OrderId}", orderId);
            return false;
        }
    }

    /// <summary>
    /// Fetches payment details from Razorpay to verify payment status.
    /// </summary>
    public async Task<PaymentDetails?> FetchPaymentDetailsAsync(string paymentId)
    {
        if (!IsConfigured)
        {
            _logger.LogError("Razorpay is not configured");
            return null;
        }

        try
        {
            return await Task.Run(() =>
            {
                var client = new RazorpayClient(_keyId, _keySecret);
                var payment = client.Payment.Fetch(paymentId);

                return new PaymentDetails
                {
                    PaymentId = payment["id"].ToString(),
                    OrderId = payment["order_id"]?.ToString() ?? "",
                    Status = payment["status"]?.ToString() ?? "",
                    Amount = Convert.ToDecimal(payment["amount"]) / 100, // Convert from paise
                    Currency = payment["currency"]?.ToString() ?? "INR",
                    Method = payment["method"]?.ToString() ?? "",
                    Email = payment["email"]?.ToString() ?? "",
                    Contact = payment["contact"]?.ToString() ?? ""
                };
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch payment details for payment: {PaymentId}", paymentId);
            return null;
        }
    }

    /// <summary>
    /// Gets payment options for Razorpay Checkout.js
    /// </summary>
    public Dictionary<string, object> GetPaymentOptions(decimal amount, string orderId, string customerName, string customerEmail, string customerContact)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Razorpay is not configured");
        }

        return new Dictionary<string, object>
        {
            { "key", _keyId },
            { "amount", (int)(amount * 100) }, // Amount in paise
            { "currency", "INR" },
            { "name", "ZenithShop" },
            { "description", $"Order #{orderId}" },
            { "order_id", orderId },
            { "prefill", new Dictionary<string, string>
                {
                    { "name", customerName },
                    { "email", customerEmail },
                    { "contact", customerContact }
                }
            },
            { "notes", new Dictionary<string, string>
                {
                    { "internal_order_id", orderId }
                }
            },
            { "theme", new Dictionary<string, string>
                {
                    { "color", "#2874f0" }
                }
            }
        };
    }
}

public class PaymentDetails
{
    public string PaymentId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
}

