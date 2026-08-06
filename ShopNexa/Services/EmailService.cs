using System.Net;
using System.Net.Mail;
using ShopNexa.Models;

namespace ShopNexa.Services;

public class EmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        try
        {
            var (smtpServer, smtpPort, enableSsl, smtpUsername, smtpPassword, fromEmail, fromName) = GetSmtpConfig();

            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
            {
                _logger.LogWarning("Email configuration not set. Skipping email to {Email}.", toEmail);
                return;
            }

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(smtpUsername, smtpPassword)
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail ?? smtpUsername, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent to {Email} with subject {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
        }
    }

    public async Task SendOrderConfirmationEmailAsync(string toEmail, string customerName, Order order)
    {
        try
        {
            var (smtpServer, smtpPort, enableSsl, smtpUsername, smtpPassword, fromEmail, fromName) = GetSmtpConfig();

            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
            {
                _logger.LogWarning("Email configuration not set. Skipping email notification.");
                return;
            }

            var subject = $"Order Confirmation - Order #{order.Id}";
            var body = BuildOrderConfirmationBody(customerName, order);

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(smtpUsername, smtpPassword)
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail ?? smtpUsername, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("Order confirmation email sent to {Email} for order {OrderId}", toEmail, order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order confirmation email to {Email}", toEmail);
        }
    }

    public async Task SendAdminOrderNotificationAsync(Order order, string customerEmail)
    {
        try
        {
            var (smtpServer, smtpPort, enableSsl, smtpUsername, smtpPassword, fromEmail, fromName) = GetSmtpConfig();
            var adminEmail = _configuration["SmtpSettings:AdminEmail"] ?? _configuration["Email:AdminEmail"];

            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword) || string.IsNullOrEmpty(adminEmail))
            {
                _logger.LogWarning("Email configuration not set. Skipping admin notification.");
                return;
            }

            var subject = $"New Order Received - Order #{order.Id}";
            var body = BuildAdminNotificationBody(order, customerEmail);

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(smtpUsername, smtpPassword)
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail ?? smtpUsername, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(adminEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("Admin notification email sent for order {OrderId}", order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin notification email for order {OrderId}", order.Id);
        }
    }

    private (string? SmtpServer, int SmtpPort, bool EnableSsl, string? SmtpUsername, string? SmtpPassword, string? FromEmail, string FromName) GetSmtpConfig()
    {
        var smtpServer = _configuration["SmtpSettings:Host"] ?? _configuration["Email:SmtpServer"];
        var portValue = _configuration["SmtpSettings:Port"] ?? _configuration["Email:SmtpPort"] ?? "587";
        var smtpPort = int.TryParse(portValue, out var parsedPort) ? parsedPort : 587;

        var enableSslValue = _configuration["SmtpSettings:EnableSsl"];
        var enableSsl = true;
        if (!string.IsNullOrEmpty(enableSslValue) && bool.TryParse(enableSslValue, out var parsedSsl))
        {
            enableSsl = parsedSsl;
        }

        var smtpUsername = _configuration["SmtpSettings:UserName"] ?? _configuration["Email:SmtpUsername"];
        var smtpPassword = _configuration["SmtpSettings:Password"] ?? _configuration["Email:SmtpPassword"];
        var fromEmail = _configuration["Email:FromEmail"] ?? smtpUsername;
        var fromName = _configuration["Email:FromName"] ?? "Zenith Shop";

        return (smtpServer, smtpPort, enableSsl, smtpUsername, smtpPassword, fromEmail, fromName);
    }

    private string BuildOrderConfirmationBody(string customerName, Order order)
    {
        var itemsHtml = string.Join("", order.Items.Select(item => $@"
            <tr>
                <td style='padding: 10px; border-bottom: 1px solid #eee;'>{item.Product?.Name}</td>
                <td style='padding: 10px; border-bottom: 1px solid #eee; text-align: center;'>{item.Quantity}</td>
                <td style='padding: 10px; border-bottom: 1px solid #eee; text-align: right;'>₹{item.UnitPrice:N2}</td>
                <td style='padding: 10px; border-bottom: 1px solid #eee; text-align: right;'>₹{(item.Quantity * item.UnitPrice):N2}</td>
            </tr>
        "));

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #000; color: #fff; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background: #f9f9f9; }}
        .order-details {{ background: #fff; padding: 20px; margin: 20px 0; }}
        table {{ width: 100%; border-collapse: collapse; }}
        .total {{ font-weight: bold; font-size: 1.2em; text-align: right; margin-top: 20px; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 0.9em; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>ZENITH</h1>
            <p>Order Confirmation</p>
        </div>
        <div class='content'>
            <p>Dear {customerName},</p>
            <p>Thank you for your order! We've received your order and are processing it now.</p>
            
            <div class='order-details'>
                <h3>Order #{order.Id}</h3>
                <p><strong>Order Date:</strong> {order.CreatedAt:MMMM dd, yyyy}</p>
                <p><strong>Payment Method:</strong> {order.PaymentMethod}</p>
                <p><strong>Payment Status:</strong> {order.PaymentStatus}</p>
                
                <h4>Order Items:</h4>
                <table>
                    <thead>
                        <tr style='background: #f0f0f0;'>
                            <th style='padding: 10px; text-align: left;'>Product</th>
                            <th style='padding: 10px; text-align: center;'>Qty</th>
                            <th style='padding: 10px; text-align: right;'>Price</th>
                            <th style='padding: 10px; text-align: right;'>Total</th>
                        </tr>
                    </thead>
                    <tbody>
                        {itemsHtml}
                    </tbody>
                </table>
                
                <div class='total'>
                    <p>Total: ₹{order.Total:N2}</p>
                </div>
            </div>
            
            <p>We'll send you another email when your order ships.</p>
            <p>If you have any questions, please contact us at mahadev010506@gmail.com</p>
        </div>
        <div class='footer'>
            <p>© 2026 Zenith Shop. All rights reserved.</p>
            <p>This is a demo site - Orders not processed</p>
        </div>
    </div>
</body>
</html>";
    }

    private string BuildAdminNotificationBody(Order order, string customerEmail)
    {
        var itemsHtml = string.Join("", order.Items.Select(item => $@"
            <tr>
                <td style='padding: 10px; border-bottom: 1px solid #eee;'>{item.Product?.Name}</td>
                <td style='padding: 10px; border-bottom: 1px solid #eee; text-align: center;'>{item.Quantity}</td>
                <td style='padding: 10px; border-bottom: 1px solid #eee; text-align: right;'>₹{item.UnitPrice:N2}</td>
            </tr>
        "));

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #000; color: #fff; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background: #f9f9f9; }}
        .alert {{ background: #fff3cd; padding: 15px; margin: 20px 0; border-left: 4px solid #ffc107; }}
        table {{ width: 100%; border-collapse: collapse; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>NEW ORDER RECEIVED</h1>
        </div>
        <div class='content'>
            <div class='alert'>
                <strong>Action Required:</strong> A new order has been placed and requires processing.
            </div>
            
            <h3>Order #{order.Id}</h3>
            <p><strong>Customer:</strong> {order.CustomerName}</p>
            <p><strong>Email:</strong> {customerEmail}</p>
            <p><strong>Order Date:</strong> {order.CreatedAt:MMMM dd, yyyy HH:mm}</p>
            <p><strong>Payment Method:</strong> {order.PaymentMethod}</p>
            <p><strong>Payment Status:</strong> {order.PaymentStatus}</p>
            
            <h4>Shipping Address:</h4>
            <p>
                {order.AddressLine1}<br>
                {(!string.IsNullOrEmpty(order.AddressLine2) ? order.AddressLine2 + "<br>" : "")}
                {order.City}, {order.PostalCode}<br>
                {order.Country}
            </p>
            
            <h4>Order Items:</h4>
            <table>
                <thead>
                    <tr style='background: #f0f0f0;'>
                        <th style='padding: 10px; text-align: left;'>Product</th>
                        <th style='padding: 10px; text-align: center;'>Qty</th>
                        <th style='padding: 10px; text-align: right;'>Price</th>
                    </tr>
                </thead>
                <tbody>
                    {itemsHtml}
                </tbody>
            </table>
            
            <h3 style='text-align: right; margin-top: 20px;'>Total: ₹{order.Total:N2}</h3>
        </div>
    </div>
</body>
</html>";
    }
}
