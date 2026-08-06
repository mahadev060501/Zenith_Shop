-- Fix Seller Roles and Approvals
-- This script ensures all users with IsSellerApproved = 1 have the Seller role
-- Run this in SQL Server Management Studio connected to ShopNexaDB
--
-- CONNECTION INFO:
-- Server: MAHADEV\SQLEXPRESS
-- Database: ShopNexaDB
--
USE [ShopNexaDB];
GO

-- Step 1: Ensure Seller role exists in AspNetRoles
IF NOT EXISTS (SELECT * FROM [AspNetRoles] WHERE [Name] = 'Seller')
BEGIN
    INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (NEWID(), 'Seller', 'SELLER', NEWID());
    PRINT 'Created Seller role';
END
ELSE
BEGIN
    PRINT 'Seller role already exists';
END
GO

-- Step 2: Add Seller role to all approved sellers who don't have it
-- This finds users with IsSellerApproved = 1 but missing the Seller role
INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
SELECT 
    u.[Id] AS [UserId],
    r.[Id] AS [RoleId]
FROM [AspNetUsers] u
CROSS JOIN [AspNetRoles] r
WHERE r.[Name] = 'Seller'
    AND u.[IsSellerApproved] = 1
    AND NOT EXISTS (
        SELECT 1 
        FROM [AspNetUserRoles] ur 
        WHERE ur.[UserId] = u.[Id] 
        AND ur.[RoleId] = r.[Id]
    );
GO

-- Step 3: Show summary
SELECT 
    'Total Approved Sellers' AS [Metric],
    COUNT(*) AS [Count]
FROM [AspNetUsers]
WHERE [IsSellerApproved] = 1

UNION ALL

SELECT 
    'Sellers with Role' AS [Metric],
    COUNT(DISTINCT ur.[UserId]) AS [Count]
FROM [AspNetUserRoles] ur
INNER JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
WHERE r.[Name] = 'Seller'
GO

PRINT 'Script completed successfully!';
PRINT 'All approved sellers now have the Seller role.';
GO




