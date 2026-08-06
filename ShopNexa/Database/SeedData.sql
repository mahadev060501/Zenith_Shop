-- =============================================
-- ShopNexa Seed Data Script
-- Microsoft SQL Server
-- =============================================
-- This script inserts initial data into the database
-- Note: Run this AFTER the application has created the tables via Entity Framework

USE [ShopNexaDB]
GO

-- =============================================
-- Insert Categories
-- =============================================
-- Only insert if categories don't exist
IF NOT EXISTS (SELECT 1 FROM Categories)
BEGIN
    INSERT INTO Categories (Name, Description, ImageUrl)
    VALUES
        ('Electronics', 'Smart devices for everyday life', 'https://images.unsplash.com/photo-1518779578993-ec3579fee39f?auto=format&fit=crop&w=800&q=80'),
        ('Fashion', 'Style that fits you', 'https://images.unsplash.com/photo-1521572267360-ee0c2909d518?auto=format&fit=crop&w=800&q=80'),
        ('Home & Living', 'Make your home cozy', 'https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=800&q=80'),
        ('Sports & Fitness', 'Gear to keep you moving', 'https://images.unsplash.com/photo-1521412644187-c49fa049e84d?auto=format&fit=crop&w=800&q=80');
    
    PRINT 'Categories inserted successfully';
END
ELSE
BEGIN
    PRINT 'Categories already exist';
END
GO

-- =============================================
-- Insert Products (5 per category)
-- =============================================
-- All products use the same image URL as requested
DECLARE @DefaultImageUrl NVARCHAR(MAX) = 'https://images.unsplash.com/photo-1505740420928-5e560c06d30e?auto=format&fit=crop&w=800&q=80';
DECLARE @ElectronicsCategoryId INT;
DECLARE @FashionCategoryId INT;
DECLARE @HomeCategoryId INT;
DECLARE @SportsCategoryId INT;

-- Get category IDs
SELECT @ElectronicsCategoryId = Id FROM Categories WHERE Name = 'Electronics';
SELECT @FashionCategoryId = Id FROM Categories WHERE Name = 'Fashion';
SELECT @HomeCategoryId = Id FROM Categories WHERE Name = 'Home & Living';
SELECT @SportsCategoryId = Id FROM Categories WHERE Name = 'Sports & Fitness';

-- Only insert if products don't exist
IF NOT EXISTS (SELECT 1 FROM Products)
BEGIN
    -- Electronics Products (5)
    INSERT INTO Products (Name, Description, Price, Stock, ImageUrl, CategoryId, SellerId)
    VALUES
        ('Wireless Earbuds Pro', 'ANC earbuds with 28-hour battery life and fast charge.', 5499.00, 60, @DefaultImageUrl, @ElectronicsCategoryId, NULL),
        ('AMOLED Smartwatch', 'Always-on display, GPS, heart-rate and SpO2 tracking.', 8999.00, 45, @DefaultImageUrl, @ElectronicsCategoryId, NULL),
        ('Wireless Charging Pad', 'Fast wireless charging pad compatible with all Qi-enabled devices.', 1999.00, 80, @DefaultImageUrl, @ElectronicsCategoryId, NULL),
        ('Bluetooth Speaker', 'Portable Bluetooth speaker with 360-degree sound and 20-hour battery.', 3499.00, 55, @DefaultImageUrl, @ElectronicsCategoryId, NULL),
        ('USB-C Hub', 'Multi-port USB-C hub with HDMI, USB 3.0, and SD card reader.', 2499.00, 70, @DefaultImageUrl, @ElectronicsCategoryId, NULL),
        
        -- Fashion Products (5)
        ('Cotton Crew Neck T-Shirt', 'Premium 180 GSM cotton tee, breathable and soft.', 699.00, 150, @DefaultImageUrl, @FashionCategoryId, NULL),
        ('Ethnic Linen Kurta', 'Lightweight linen kurta ideal for festive evenings.', 1299.00, 90, @DefaultImageUrl, @FashionCategoryId, NULL),
        ('Denim Jeans', 'Classic fit denim jeans with stretch comfort.', 1999.00, 75, @DefaultImageUrl, @FashionCategoryId, NULL),
        ('Leather Jacket', 'Genuine leather jacket with quilted lining.', 4999.00, 40, @DefaultImageUrl, @FashionCategoryId, NULL),
        ('Running Shoes', 'Lightweight running shoes with cushioned sole.', 2999.00, 100, @DefaultImageUrl, @FashionCategoryId, NULL),
        
        -- Home & Living Products (5)
        ('Terracotta Dinner Set (12 pcs)', 'Handmade terracotta dinnerware with matte finish.', 2499.00, 40, @DefaultImageUrl, @HomeCategoryId, NULL),
        ('Aroma Diffuser with LED', 'Ultrasonic diffuser with warm white ambient lighting.', 1599.00, 70, @DefaultImageUrl, @HomeCategoryId, NULL),
        ('Throw Pillow Set', 'Set of 4 decorative throw pillows with premium covers.', 1299.00, 85, @DefaultImageUrl, @HomeCategoryId, NULL),
        ('Table Lamp', 'Modern table lamp with adjustable brightness.', 899.00, 60, @DefaultImageUrl, @HomeCategoryId, NULL),
        ('Wall Clock', 'Elegant wall clock with silent movement.', 1499.00, 50, @DefaultImageUrl, @HomeCategoryId, NULL),
        
        -- Sports & Fitness Products (5)
        ('Adjustable Dumbbells Set', 'Pair adjustable up to 24kg each, compact stand.', 6999.00, 30, @DefaultImageUrl, @SportsCategoryId, NULL),
        ('Yoga Mat 6mm (Non-Slip)', 'Sweat-resistant, cushioned mat with carry strap.', 1199.00, 110, @DefaultImageUrl, @SportsCategoryId, NULL),
        ('Resistance Bands Set', 'Set of 5 resistance bands with different resistance levels.', 899.00, 95, @DefaultImageUrl, @SportsCategoryId, NULL),
        ('Jump Rope', 'Adjustable speed jump rope with weighted handles.', 499.00, 120, @DefaultImageUrl, @SportsCategoryId, NULL),
        ('Foam Roller', 'High-density foam roller for muscle recovery.', 799.00, 65, @DefaultImageUrl, @SportsCategoryId, NULL);
    
    PRINT 'Products inserted successfully';
END
ELSE
BEGIN
    PRINT 'Products already exist';
END
GO

-- =============================================
-- Create Roles (if not exists)
-- =============================================
IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE Name = 'Admin')
BEGIN
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'Admin', 'ADMIN', NEWID());
    PRINT 'Admin role created';
END

IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE Name = 'Seller')
BEGIN
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'Seller', 'SELLER', NEWID());
    PRINT 'Seller role created';
END
GO

-- =============================================
-- Verify Data
-- =============================================
PRINT '=== Database Summary ===';
PRINT 'Categories: ' + CAST((SELECT COUNT(*) FROM Categories) AS NVARCHAR(10));
PRINT 'Products: ' + CAST((SELECT COUNT(*) FROM Products) AS NVARCHAR(10));
PRINT 'Products per Category:';
SELECT c.Name, COUNT(p.Id) AS ProductCount
FROM Categories c
LEFT JOIN Products p ON c.Id = p.CategoryId
GROUP BY c.Name
ORDER BY c.Name;
GO















