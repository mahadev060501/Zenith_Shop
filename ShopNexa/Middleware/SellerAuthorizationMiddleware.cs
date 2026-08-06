using Microsoft.AspNetCore.Identity;
using ShopNexa.Models;

namespace ShopNexa.Middleware;

public class SellerAuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SellerAuthorizationMiddleware> _logger;

    public SellerAuthorizationMiddleware(RequestDelegate next, ILogger<SellerAuthorizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        // Check if the request is for seller routes
        var path = context.Request.Path.Value?.ToLower() ?? "";
        
        if (path.StartsWith("/seller") && !path.StartsWith("/seller/becomeseller") && !path.StartsWith("/seller/register") && !path.StartsWith("/seller/applicationstatus"))
        {
            var user = await userManager.GetUserAsync(context.User);
            
            if (user != null)
            {
                // Reload user to get latest data
                user = await userManager.FindByIdAsync(user.Id);
                
                if (user != null && !user.IsSellerApproved)
                {
                    _logger.LogWarning("User {UserId} attempted to access seller route without approval", user.Id);
                    context.Response.Redirect("/Seller/ApplicationStatus");
                    return;
                }
            }
        }

        await _next(context);
    }
}

// Extension method for easy registration
public static class SellerAuthorizationMiddlewareExtensions
{
    public static IApplicationBuilder UseSellerAuthorization(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SellerAuthorizationMiddleware>();
    }
}
