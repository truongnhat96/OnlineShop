IF DB_NAME() <> N'OnlineShop_Recommendation_Test' THROW 50000, 'Wrong test database', 1;
SET IDENTITY_INSERT dbo.Roles ON;
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Id=1) INSERT dbo.Roles (Id,Name) VALUES (1,N'Admin');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Id=2) INSERT dbo.Roles (Id,Name) VALUES (2,N'User');
SET IDENTITY_INSERT dbo.Roles OFF;
