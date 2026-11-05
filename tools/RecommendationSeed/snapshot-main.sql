SET NOCOUNT ON;
IF DB_NAME() <> N'OnlineShop' THROW 50000, 'Unexpected database', 1;
SELECT c.Name AS Category, p.ManufacturerCode, p.Name, p.Price, p.Quantity, p.Sold, p.FitmentKind, p.ReferenceUrl, p.RecommendationSeedKey
FROM dbo.Products p JOIN dbo.Categories c ON c.Id = p.CategoryId
WHERE p.RecommendationSeedKey LIKE 'motoshop-v1-%' ORDER BY c.Name, p.ManufacturerCode;
SELECT p.ManufacturerCode, v.Brand, v.Model, v.Variant, v.Market, f.YearFrom, f.YearTo, f.EvidenceUrl, f.Notes
FROM dbo.ProductFitments f JOIN dbo.Products p ON p.Id = f.ProductId JOIN dbo.VehicleModels v ON v.Id = f.VehicleModelId
WHERE p.RecommendationSeedKey LIKE 'motoshop-v1-%' ORDER BY p.ManufacturerCode;
