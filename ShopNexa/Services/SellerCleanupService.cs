using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShopNexa.Data;
using ShopNexa.Models;

namespace ShopNexa.Services;

public class SellerCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SellerCleanupService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromDays(1); // Check daily

    public SellerCleanupService(IServiceProvider serviceProvider, ILogger<SellerCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRemoveInactiveSellers(stoppingToken);
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in seller cleanup service");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken); // Retry after 1 hour on error
            }
        }
    }

    private async Task CheckAndRemoveInactiveSellers(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        // Get all approved sellers
        var sellers = await userManager.GetUsersInRoleAsync("Seller");
        var approvedSellers = sellers.Where(s => s.IsSellerApproved).ToList();

        foreach (var seller in approvedSellers)
        {
            // Check if seller has any orders with items sold in the last 7 days
            var hasRecentSales = await context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .AnyAsync(oi => 
                    oi.Product != null && 
                    oi.Product.SellerId == seller.Id &&
                    oi.Order != null &&
                    oi.Order.CreatedAt >= sevenDaysAgo &&
                    oi.Order.Status != "Cancelled" &&
                    oi.Order.Status != "Returned",
                    cancellationToken);

            if (!hasRecentSales)
            {
                // Check when seller was approved
                var sellerProfile = await context.SellerProfiles
                    .FirstOrDefaultAsync(sp => sp.UserId == seller.Id, cancellationToken);

                if (sellerProfile != null && sellerProfile.CreatedAt < sevenDaysAgo)
                {
                    // Only remove if seller was approved more than 7 days ago and has no recent sales
                    // Remove seller role and approval
                    await userManager.RemoveFromRoleAsync(seller, "Seller");
                    seller.IsSellerApproved = false;
                    var result = await userManager.UpdateAsync(seller);
                    if (!result.Succeeded)
                    {
                        _logger.LogError($"Failed to update seller {seller.Email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    }
                    
                    // Update seller profile to inactive
                    sellerProfile.IsActive = false;
                    await context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation($"Removed seller role from {seller.Email} - no sales in 7 days");
                }
            }
        }
    }
}