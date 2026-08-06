using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ShopNexa.Models;
using ShopNexa.Services;
using System.Security.Claims;

namespace ShopNexa.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountController> _logger;
    private readonly CartService _cartService;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AccountController> logger,
        CartService cartService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _cartService = cartService;
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            _logger.LogInformation("User created a new account with password.");
            TempData["Success"] = "Registration successful! Welcome to ZenithShop.";
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("User logged in.");
            TempData["Success"] = "Welcome back!";
            return RedirectToLocal(returnUrl);
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // Get user ID before signing out
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Clear cart from database for this user
        if (!string.IsNullOrEmpty(userId))
        {
            await _cartService.ClearCartForUserAsync(userId);
        }

        // Clear session
        HttpContext.Session.Clear();

        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        return View(user);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(string fullName, string phoneNumber)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        user.FullName = fullName;
        user.PhoneNumber = phoneNumber;
        
        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            TempData["Success"] = "Profile updated successfully!";
        }
        else
        {
            TempData["Error"] = "Failed to update profile.";
        }

        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        return View(user);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        if (newPassword != confirmPassword)
        {
            TempData["Error"] = "New password and confirmation password do not match.";
            return RedirectToAction(nameof(Settings));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded)
        {
            TempData["Success"] = "Password changed successfully!";
        }
        else
        {
            TempData["Error"] = result.Errors.FirstOrDefault()?.Description ?? "Failed to change password.";
        }

        return RedirectToAction(nameof(Settings));
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }

    // JWT Token Endpoint for API Authentication
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> GetToken([FromBody] LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { message = "Invalid credentials" });
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
        if (!result.Succeeded)
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var jwtService = HttpContext.RequestServices.GetRequiredService<JwtService>();
        var token = jwtService.GenerateToken(user, roles);

        return Ok(new
        {
            token = token,
            expiresIn = 604800, // 7 days in seconds
            user = new
            {
                id = user.Id,
                email = user.Email,
                fullName = user.FullName,
                roles = roles,
                isSellerApproved = user.IsSellerApproved
            }
        });
    }

    // Validate Token Endpoint
    [HttpGet]
    [Authorize]
    public IActionResult ValidateToken()
    {
        return Ok(new
        {
            valid = true,
            user = new
            {
                id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                email = User.FindFirst(ClaimTypes.Email)?.Value,
                name = User.FindFirst(ClaimTypes.Name)?.Value,
                roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
            }
        });
    }
}

