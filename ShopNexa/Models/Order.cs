using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopNexa.Models;

public class Order
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(50)]
    public string Status { get; set; } = "Pending";

    [Required, StringLength(160)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string AddressLine1 { get; set; } = string.Empty;

    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [Required, StringLength(80)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string Country { get; set; } = string.Empty;

    [StringLength(30)]
    public string? PostalCode { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Total { get; set; }

    // Payment meta
    [StringLength(50)]
    public string PaymentMethod { get; set; } = "COD"; // "Razorpay" or "COD"

    [StringLength(100)]
    public string? PaymentStatus { get; set; } = "Pending"; // Pending / Paid / Failed

    // Razorpay fields
    [StringLength(200)]
    public string? RazorpayOrderId { get; set; }

    [StringLength(200)]
    public string? RazorpayPaymentId { get; set; }

    [StringLength(500)]
    public string? RazorpaySignature { get; set; }

    public DateTime? PaymentDate { get; set; }

    // Generic transaction id used mainly for refunds / legacy flows
    [StringLength(200)]
    public string? TransactionId { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? RefundAmount { get; set; }

    [StringLength(100)]
    public string? RefundStatus { get; set; }

    [StringLength(200)]
    public string? RefundTransactionId { get; set; }

    public DateTime? RefundDate { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Range(1, 999)]
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal UnitPrice { get; set; }

    [StringLength(50)]
    public string? ReturnStatus { get; set; }

    public DateTime? ReturnRequestDate { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? RefundAmount { get; set; }
}

