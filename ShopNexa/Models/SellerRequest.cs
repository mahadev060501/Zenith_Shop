using System.ComponentModel.DataAnnotations;

namespace ShopNexa.Models;

public class SellerRequest
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, StringLength(200)]
    [Display(Name = "Company Name")]
    public string CompanyName { get; set; } = string.Empty;

    [Required, StringLength(200)]
    [Display(Name = "Owner Name")]
    public string OwnerName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, Phone, StringLength(15)]
    [Display(Name = "Phone Number")]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Address { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "GST Number")]
    public string? GSTNumber { get; set; }

    [StringLength(500)]
    [Display(Name = "Bank Account Details")]
    public string? BankAccountDetails { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    [StringLength(500)]
    [Display(Name = "Admin Notes")]
    public string? AdminNotes { get; set; }

    [StringLength(200)]
    public string? ReviewedBy { get; set; }
}
