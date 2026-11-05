-- TEST ONLY: changes are rolled back; never run against the business database.
IF DB_NAME() <> N'OnlineShop_SeedExport_Test' THROW 50000, 'Wrong test database', 1;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DELETE FROM dbo.Products WHERE RecommendationSeedKey LIKE N'motoshop-v1-%';
GO
:r tools/RecommendationSeed/seed-40-products.sql
GO
IF (SELECT COUNT(*) FROM dbo.Products WHERE RecommendationSeedKey LIKE N'motoshop-v1-%') <> 40
    THROW 50001, 'Expected 40 products', 1;
IF (SELECT COUNT(*) FROM dbo.ProductFitments f JOIN dbo.Products p ON p.Id=f.ProductId WHERE p.RecommendationSeedKey LIKE N'motoshop-v1-%') <> 15
    THROW 50002, 'Expected 15 fitments', 1;
ROLLBACK TRANSACTION;
PRINT 'Transactional test passed; test database restored.';
