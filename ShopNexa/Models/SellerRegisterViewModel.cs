using System.ComponentModel.DataAnnotations;

namespace ShopNexa.Models
{
    public class SellerRegisterViewModel
    {
        [Required]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        public string OwnerName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        public string? GSTNumber { get; set; }

        public string? BankAccountDetails { get; set; }
    }
}