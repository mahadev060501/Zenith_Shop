using Microsoft.AspNetCore.Identity;

namespace ShopNexa.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public bool IsSellerApproved { get; set; } = false;
    public DateTime? SellerApplicationDate { get; set; }
}

