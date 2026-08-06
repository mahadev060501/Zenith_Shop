using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopNexa.Data;
using ShopNexa.Models;

namespace ShopNexa.Controllers;

[Authorize]
public class FeedbackController : Controller
{
    private readonly ApplicationDbContext _context;
    public FeedbackController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int orderId, int rating, string? message)
    {
        if (rating < 1 || rating > 5)
        {
            return BadRequest("Rating must be between 1 and 5.");
        }

        if (message != null && message.Length > 500)
        {
            return BadRequest("Message cannot exceed 500 characters.");
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // Check if feedback already exists for this order
        var existingFeedback = await _context.Feedbacks
            .FirstOrDefaultAsync(f => f.OrderId == orderId && f.UserId == userId);

        if (existingFeedback != null)
        {
            return Json(new { success = false, message = "You have already submitted feedback for this order." });
        }

        // Verify the order belongs to the user
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

        if (order == null)
        {
            return NotFound("Order not found or does not belong to you.");
        }

        // Create and save the feedback
        var feedback = new Feedback
        {
            OrderId = orderId,
            UserId = userId,
            Rating = rating,
            Message = message,
            CreatedAt = DateTime.UtcNow
        };

        _context.Feedbacks.Add(feedback);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Thank you for your feedback!" });
    }

    [HttpGet]
    public async Task<IActionResult> HasSubmittedFeedback(int orderId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var hasSubmitted = await _context.Feedbacks
            .AnyAsync(f => f.OrderId == orderId && f.UserId == userId);

        return Json(new { hasSubmitted = hasSubmitted });
    }
}