using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ShopNexa.Models;

namespace ShopNexa.Controllers;

[Authorize(Roles = "Admin")]
public class RoleSwitchController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public RoleSwitchController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _environment = environment;
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Index()
    {
        // DEV-ONLY: Disable in production
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        // Additional check via configuration
        var allowRoleSwitch = _configuration.GetValue<bool>("DevSettings:AllowRoleSwitch", false);
        if (!allowRoleSwitch && !_environment.IsDevelopment())
        {
            return NotFound();
        }

        ViewBag.CurrentUser = User.Identity?.Name;
        ViewBag.CurrentRoles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchRole(string role)
    {
        // DEV-ONLY: Disable in production
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var allowRoleSwitch = _configuration.GetValue<bool>("DevSettings:AllowRoleSwitch", false);
        if (!allowRoleSwitch && !_environment.IsDevelopment())
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        // Valid roles
        var validRoles = new[] { "User", "Seller", "Admin" };
        if (!validRoles.Contains(role))
        {
            TempData["Error"] = "Invalid role selected.";
            return RedirectToAction(nameof(Index));
        }

        // Remove all existing roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Any())
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        // Ensure role exists
        if (!await _roleManager.RoleExistsAsync(role))
        {
            await _roleManager.CreateAsync(new IdentityRole(role));
        }

        // Add new role
        await _userManager.AddToRoleAsync(user, role);

        // If switching to Seller, auto-approve for dev purposes
        if (role == "Seller")
        {
            user.IsSellerApproved = true;
            await _userManager.UpdateAsync(user);
        }

        TempData["Success"] = $"Role switched to {role} successfully. Please log out and log back in for changes to take effect.";
        return RedirectToAction(nameof(Index));
    }
}







