-- DIRECT SELLER FIX - Run this for a specific user
-- Replace 'YOUR_EMAIL@example.com' with the actual user email
--
-- CONNECTION INFO:
-- Server: MAHADEV\SQLEXPRESS
-- Database: ShopNexaDB
--
USE [ShopNexaDB];
GO

-- SET YOUR EMAIL HERE
DECLARE @UserEmail NVARCHAR(256) = 'YOUR_EMAIL@example.com'; -- CHANGE THIS!
DECLARE @UserId NVARCHAR(450);
DECLARE @RoleId NVARCHAR(450);

-- Get User ID
SELECT @UserId = [Id] FROM [AspNetUsers] WHERE [Email] = @UserEmail;

IF @UserId IS NULL
BEGIN
    PRINT 'ERROR: User with email ' + @UserEmail + ' not found!';
    PRINT 'Please check the email address and try again.';
    RETURN;
END

PRINT 'Found user: ' + @UserEmail + ' (ID: ' + @UserId + ')';

-- Step 1: Ensure Seller role exists
IF NOT EXISTS (SELECT * FROM [AspNetRoles] WHERE [Name] = 'Seller')
BEGIN
    INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (NEWID(), 'Seller', 'SELLER', NEWID());
    PRINT 'Created Seller role';
END

-- Get Role ID
SELECT @RoleId = [Id] FROM [AspNetRoles] WHERE [Name] = 'Seller';

-- Step 2: Add Seller role to user (if not already added)
IF NOT EXISTS (SELECT * FROM [AspNetUserRoles] WHERE [UserId] = @UserId AND [RoleId] = @RoleId)
BEGIN
    INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
    VALUES (@UserId, @RoleId);
    PRINT 'Added Seller role to user';
END
ELSE
BEGIN
    PRINT 'User already has Seller role';
END

-- Step 3: Approve seller in user table
UPDATE [AspNetUsers]
SET [IsSellerApproved] = 1,
    [SellerApplicationDate] = GETUTCDATE()
WHERE [Id] = @UserId;

PRINT 'Updated user approval status';

-- Step 4: Create/Update Seller Application
IF NOT EXISTS (SELECT * FROM [SellerApplications] WHERE [UserId] = @UserId)
BEGIN
    INSERT INTO [SellerApplications] ([UserId], [BusinessName], [Status], [CreatedAt], [ReviewedAt])
    VALUES (@UserId, 'Direct Fix - ' + @UserEmail, 'Approved', GETUTCDATE(), GETUTCDATE());
    PRINT 'Created seller application';
END
ELSE
BEGIN
    UPDATE [SellerApplications]
    SET [Status] = 'Approved',
        [ReviewedAt] = GETUTCDATE()
    WHERE [UserId] = @UserId;
    PRINT 'Updated seller application status';
END

-- Step 5: Verify
PRINT '';
PRINT '=== VERIFICATION ===';
SELECT 
    u.[Email],
    u.[IsSellerApproved],
    u.[SellerApplicationDate],
    CASE WHEN ur.[RoleId] IS NOT NULL THEN 'Yes' ELSE 'No' END AS [HasSellerRole],
    sa.[Status] AS [ApplicationStatus]
FROM [AspNetUsers] u
LEFT JOIN [AspNetUserRoles] ur ON u.[Id] = ur.[UserId] AND ur.[RoleId] = @RoleId
LEFT JOIN [SellerApplications] sa ON u.[Id] = sa.[UserId]
WHERE u.[Id] = @UserId;

PRINT '';
PRINT 'Fix completed! User should now be able to access Seller Dashboard.';
PRINT 'Please log out and log back in for changes to take effect.';
GO




