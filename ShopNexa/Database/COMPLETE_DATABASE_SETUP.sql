-- =============================================
-- ShopNexa COMPLETE DATABASE SETUP
-- Single File - Run This Once to Setup Everything
-- Microsoft SQL Server
-- =============================================
-- 
-- CONNECTION INFO:
-- Server: MAHADEV\SQLEXPRESS
-- Database: ShopNexaDB
--
-- INSTRUCTIONS:
-- 1. Open SQL Server Management Studio
-- 2. Connect to: MAHADEV\SQLEXPRESS
-- 3. Create database if not exists: CREATE DATABASE ShopNexaDB;
-- 4. Select database: USE ShopNexaDB;
-- 5. Run this entire script (F5)
-- 6. Done! Your database is ready.
-- =============================================

USE [ShopNexaDB];
GO

PRINT '========================================';
PRINT 'ShopNexa Complete Database Setup';
PRINT '========================================';
PRINT '';

-- =============================================
-- STEP 1: Add Missing Columns to AspNetUsers
-- =============================================
PRINT 'Step 1: Adding missing columns to AspNetUsers...';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND name = 'FullName')
BEGIN
    ALTER TABLE [AspNetUsers] ADD [FullName] NVARCHAR(256) NULL;
    PRINT '  ✓ Added FullName column';
END
ELSE
    PRINT '  ✓ FullName column already exists';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND name = 'IsSellerApproved')
BEGIN
    ALTER TABLE [AspNetUsers] ADD [IsSellerApproved] BIT NOT NULL DEFAULT 0;
    PRINT '  ✓ Added IsSellerApproved column';
END
ELSE
    PRINT '  ✓ IsSellerApproved column already exists';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND name = 'SellerApplicationDate')
BEGIN
    ALTER TABLE [AspNetUsers] ADD [SellerApplicationDate] DATETIME2 NULL;
    PRINT '  ✓ Added SellerApplicationDate column';
END
ELSE
    PRINT '  ✓ SellerApplicationDate column already exists';

GO

-- =============================================
-- STEP 2: Create SellerApplications Table
-- =============================================
PRINT '';
PRINT 'Step 2: Creating SellerApplications table...';

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SellerApplications')
BEGIN
    CREATE TABLE [SellerApplications] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [UserId] NVARCHAR(450) NOT NULL,
        [BusinessName] NVARCHAR(200) NOT NULL,
        [BusinessDescription] NVARCHAR(500) NULL,
        [GSTNumber] NVARCHAR(50) NULL,
        [BusinessAddress] NVARCHAR(200) NULL,
        [PhoneNumber] NVARCHAR(15) NULL,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [ReviewedAt] DATETIME2 NULL,
        [AdminNotes] NVARCHAR(500) NULL,
        FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE
    );
    PRINT '  ✓ SellerApplications table created';
END
ELSE
    PRINT '  ✓ SellerApplications table already exists';

GO

-- =============================================
-- STEP 3: Add Missing Columns to Products
-- =============================================
PRINT '';
PRINT 'Step 3: Adding missing columns to Products...';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Products]') AND name = 'OriginalPrice')
BEGIN
    ALTER TABLE [Products] ADD [OriginalPrice] DECIMAL(10,2) NULL;
    PRINT '  ✓ Added OriginalPrice column';
END
ELSE
    PRINT '  ✓ OriginalPrice column already exists';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Products]') AND name = 'SellerId')
BEGIN
    ALTER TABLE [Products] ADD [SellerId] NVARCHAR(450) NULL;
    ALTER TABLE [Products] ADD CONSTRAINT [FK_Products_AspNetUsers_SellerId] 
        FOREIGN KEY ([SellerId]) REFERENCES [AspNetUsers]([Id]) ON DELETE SET NULL;
    PRINT '  ✓ Added SellerId column';
END
ELSE
    PRINT '  ✓ SellerId column already exists';

GO

-- =============================================
-- STEP 4: Add Missing Columns to Orders
-- =============================================
PRINT '';
PRINT 'Step 4: Adding missing columns to Orders...';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'CreatedAt')
BEGIN
    ALTER TABLE [Orders] ADD [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE();
    PRINT '  ✓ Added CreatedAt column';
END
ELSE
    PRINT '  ✓ CreatedAt column already exists';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'PaymentStatus')
BEGIN
    ALTER TABLE [Orders] ADD [PaymentStatus] NVARCHAR(100) NULL;
    PRINT '  ✓ Added PaymentStatus column';
END
ELSE
    PRINT '  ✓ PaymentStatus column already exists';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'RefundAmount')
BEGIN
    ALTER TABLE [Orders] ADD [RefundAmount] DECIMAL(10,2) NULL;
    PRINT '  ✓ Added RefundAmount column';
END
ELSE
    PRINT '  ✓ RefundAmount column already exists';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'RefundStatus')
BEGIN
    ALTER TABLE [Orders] ADD [RefundStatus] NVARCHAR(100) NULL;
    PRINT '  ✓ Added RefundStatus column';
END
ELSE
    PRINT '  ✓ RefundStatus column already exists';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'RefundTransactionId')
BEGIN
    ALTER TABLE [Orders] ADD [RefundTransactionId] NVARCHAR(200) NULL;
    PRINT '  ✓ Added RefundTransactionId column';
END
ELSE
    PRINT '  ✓ RefundTransactionId column already exists';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'RefundDate')
BEGIN
    ALTER TABLE [Orders] ADD [RefundDate] DATETIME2 NULL;
    PRINT '  ✓ Added RefundDate column';
END
ELSE
    PRINT '  ✓ RefundDate column already exists';

GO

-- =============================================
-- STEP 5: Add Missing Columns to OrderItems
-- =============================================
PRINT '';
PRINT 'Step 5: Adding missing columns to OrderItems...';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrderItems]') AND name = 'ReturnStatus')
BEGIN
    ALTER TABLE [OrderItems] ADD [ReturnStatus] NVARCHAR(50) NULL;
    PRINT '  ✓ Added ReturnStatus column';
END
ELSE
    PRINT '  ✓ ReturnStatus column already exists';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrderItems]') AND name = 'ReturnRequestDate')
BEGIN
    ALTER TABLE [OrderItems] ADD [ReturnRequestDate] DATETIME2 NULL;
    PRINT '  ✓ Added ReturnRequestDate column';
END
ELSE
    PRINT '  ✓ ReturnRequestDate column already exists';

GO

-- =============================================
-- STEP 6: Fix NULL Values in Orders
-- =============================================
PRINT '';
PRINT 'Step 6: Fixing NULL values in Orders...';

UPDATE [Orders]
SET [CreatedAt] = GETUTCDATE()
WHERE [CreatedAt] IS NULL OR [CreatedAt] = '0001-01-01';

UPDATE [Orders]
SET [Status] = 'Pending'
WHERE [Status] IS NULL OR [Status] = '';

UPDATE [Orders]
SET [CustomerName] = 'Unknown Customer'
WHERE [CustomerName] IS NULL OR [CustomerName] = '';

UPDATE [Orders]
SET [Email] = ''
WHERE [Email] IS NULL;

UPDATE [Orders]
SET [AddressLine1] = ''
WHERE [AddressLine1] IS NULL OR [AddressLine1] = '';

UPDATE [Orders]
SET [City] = ''
WHERE [City] IS NULL OR [City] = '';

UPDATE [Orders]
SET [Country] = ''
WHERE [Country] IS NULL OR [Country] = '';

UPDATE [Orders]
SET [PaymentMethod] = 'CashOnDelivery'
WHERE [PaymentMethod] IS NULL OR [PaymentMethod] = '';

PRINT '  ✓ Fixed NULL values in Orders table';

GO

-- =============================================
-- STEP 7: Create Roles
-- =============================================
PRINT '';
PRINT 'Step 7: Creating roles...';

IF NOT EXISTS (SELECT * FROM [AspNetRoles] WHERE [Name] = 'Admin')
BEGIN
    INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (NEWID(), 'Admin', 'ADMIN', NEWID());
    PRINT '  ✓ Created Admin role';
END
ELSE
    PRINT '  ✓ Admin role already exists';

IF NOT EXISTS (SELECT * FROM [AspNetRoles] WHERE [Name] = 'Seller')
BEGIN
    INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (NEWID(), 'Seller', 'SELLER', NEWID());
    PRINT '  ✓ Created Seller role';
END
ELSE
    PRINT '  ✓ Seller role already exists';

GO

-- =============================================
-- STEP 8: Seed Categories (if not exist)
-- =============================================
PRINT '';
PRINT 'Step 8: Seeding categories...';

IF NOT EXISTS (SELECT 1 FROM [Categories])
BEGIN
    INSERT INTO [Categories] ([Name], [Description], [ImageUrl])
    VALUES
        ('Electronics', 'Smart devices for everyday life', 'https://images.unsplash.com/photo-1518779578993-ec3579fee39f?auto=format&fit=crop&w=800&q=80'),
        ('Fashion', 'Style that fits you', 'https://images.unsplash.com/photo-1521572267360-ee0c2909d518?auto=format&fit=crop&w=800&q=80'),
        ('Home & Living', 'Make your home cozy', 'https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=800&q=80'),
        ('Sports & Fitness', 'Gear to keep you moving', 'https://images.unsplash.com/photo-1521412644187-c49fa049e84d?auto=format&fit=crop&w=800&q=80');
    PRINT '  ✓ Categories seeded';
END
ELSE
    PRINT '  ✓ Categories already exist';

GO

-- =============================================
-- STEP 9: Fix Seller Roles (Add role to approved sellers)
-- =============================================
PRINT '';
PRINT 'Step 9: Fixing seller roles...';

DECLARE @SellerRoleId NVARCHAR(450);
SELECT @SellerRoleId = [Id] FROM [AspNetRoles] WHERE [Name] = 'Seller';

IF @SellerRoleId IS NOT NULL
BEGIN
    INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
    SELECT 
        u.[Id] AS [UserId],
        @SellerRoleId AS [RoleId]
    FROM [AspNetUsers] u
    WHERE u.[IsSellerApproved] = 1
        AND NOT EXISTS (
            SELECT 1 
            FROM [AspNetUserRoles] ur 
            WHERE ur.[UserId] = u.[Id] 
            AND ur.[RoleId] = @SellerRoleId
        );
    
    DECLARE @FixedCount INT = @@ROWCOUNT;
    IF @FixedCount > 0
        PRINT '  ✓ Fixed ' + CAST(@FixedCount AS VARCHAR) + ' seller role(s)';
    ELSE
        PRINT '  ✓ All seller roles are correct';
END

GO

-- =============================================
-- STEP 10: Summary
-- =============================================
PRINT '';
PRINT '========================================';
PRINT 'Setup Complete!';
PRINT '========================================';
PRINT '';
PRINT 'Database is ready to use.';
PRINT '';
PRINT 'Summary:';
PRINT '  ✓ All required columns added';
PRINT '  ✓ All tables created';
PRINT '  ✓ Roles created';
PRINT '  ✓ Categories seeded';
PRINT '  ✓ Seller roles fixed';
PRINT '';
PRINT 'You can now start your application!';
PRINT '========================================';
GO




