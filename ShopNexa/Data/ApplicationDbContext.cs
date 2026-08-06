using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShopNexa.Models;

namespace ShopNexa.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<SellerRequest> SellerRequests => Set<SellerRequest>();
    public DbSet<SellerProfile> SellerProfiles => Set<SellerProfile>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Ensure Product.SellerId is properly mapped to ApplicationUser and kept consistent
        builder.Entity<Product>()
            .HasOne(p => p.Seller)
            .WithMany()
            .HasForeignKey(p => p.SellerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure CartItem entity
        builder.Entity<CartItem>()
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Feedback entity
        builder.Entity<Feedback>()
            .HasOne(f => f.Order)
            .WithMany()
            .HasForeignKey(f => f.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Feedback>()
            .HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CartItem>()
            .HasIndex(c => new { c.UserId, c.ProductId })
            .IsUnique();

        builder.Entity<CartItem>()
            .Property(c => c.Price)
            .HasPrecision(10, 2);

        // Configure SellerRequest entity
        builder.Entity<SellerRequest>()
            .HasOne(sr => sr.User)
            .WithMany()
            .HasForeignKey(sr => sr.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SellerRequest>()
            .HasIndex(sr => sr.Email)
            .IsUnique();

        builder.Entity<SellerRequest>()
            .HasIndex(sr => sr.Phone)
            .IsUnique();

        // Configure SellerProfile entity
        builder.Entity<SellerProfile>()
            .HasOne(sp => sp.User)
            .WithMany()
            .HasForeignKey(sp => sp.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SellerProfile>()
            .HasIndex(sp => sp.Email)
            .IsUnique();

        builder.Entity<SellerProfile>()
            .HasIndex(sp => sp.UserId)
            .IsUnique();

        // Configure SellerProfile decimal properties
        builder.Entity<SellerProfile>()
            .Property(sp => sp.TotalRevenue)
            .HasColumnType("decimal(18,2)");
    }
}