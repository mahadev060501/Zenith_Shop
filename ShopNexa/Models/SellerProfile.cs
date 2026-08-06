using System.ComponentModel.DataAnnotations;

namespace ShopNexa.Models;

public class SellerProfile
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

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Statistics
    public int TotalProducts { get; set; } = 0;
    public int TotalOrders { get; set; } = 0;
    public decimal TotalRevenue { get; set; } = 0;

}
