/*
  40 sản phẩm MotoShop, được xuất từ database đang dùng.
  Chạy trên SQL Server, trong database đích đã áp dụng migration
  20261104110652_MotorcycleRecommendations. Chọn đúng database trước khi chạy.
  File không chứa tài khoản, đơn hàng, coupon hoặc đánh giá.
  Giá/tồn kho và cờ IsSampleData phản ánh dữ liệu lúc xuất; ảnh seed là minh họa.
  Sao chép thêm Infrastructure/wwwroot/uploads/products/motoshop-seed-illustration.svg
  cùng các tệp ảnh khác nếu sản phẩm đã được đổi ảnh trong admin.
  Chạy lại sẽ bỏ qua sản phẩm đã có cùng RecommendationSeedKey, không ghi đè.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.Products', N'U') IS NULL OR OBJECT_ID(N'dbo.Categories', N'U') IS NULL
 OR OBJECT_ID(N'dbo.VehicleModels', N'U') IS NULL OR OBJECT_ID(N'dbo.ProductFitments', N'U') IS NULL
 OR COL_LENGTH(N'dbo.Products', N'RecommendationSeedKey') IS NULL
    THROW 50000, 'Database chua co schema goi y. Hay ap dung migration truoc.', 1;

DECLARE @products nvarchar(max) = N'__PRODUCTS_JSON__';
DECLARE @vehicles nvarchar(max) = N'__VEHICLES_JSON__';
DECLARE @fitments nvarchar(max) = N'__FITMENTS_JSON__';

SELECT * INTO #products FROM OPENJSON(@products) WITH (
    Name nvarchar(100), Brand nvarchar(50), Quantity int, Date_Import datetime2,
    Price float, Sold int, Category nvarchar(100), Description nvarchar(max), ImageUrl nvarchar(max),
    OldPrice float, FitmentKind int, IsSampleData bit, ManufacturerCode nvarchar(100),
    RecommendationSeedKey nvarchar(120), RecommendationTags nvarchar(2000),
    RecommendationUseCases nvarchar(500), ReferenceUrl nvarchar(2048)
);
SELECT * INTO #vehicles FROM OPENJSON(@vehicles) WITH (
    Brand nvarchar(50), Model nvarchar(100), Variant nvarchar(100), Market nvarchar(20), Aliases nvarchar(500)
);
SELECT * INTO #fitments FROM OPENJSON(@fitments) WITH (
    RecommendationSeedKey nvarchar(120), Brand nvarchar(50), Model nvarchar(100), Variant nvarchar(100),
    Market nvarchar(20), YearFrom int, YearTo int, EvidenceUrl nvarchar(2048), Notes nvarchar(1000)
);
IF (SELECT COUNT(*) FROM #products) <> 40 OR EXISTS (SELECT 1 FROM #products WHERE RecommendationSeedKey IS NULL OR Category IS NULL)
 OR EXISTS (SELECT RecommendationSeedKey FROM #products GROUP BY RecommendationSeedKey HAVING COUNT(*) > 1)
    THROW 50001, 'Du lieu san pham khong hop le; khong thuc hien.', 1;
IF EXISTS (SELECT 1 FROM #vehicles GROUP BY Brand, Model, Variant, Market HAVING COUNT(*) > 1)
    THROW 50002, 'Danh sach xe trung; khong thuc hien.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS (SELECT c.Name FROM dbo.Categories c JOIN (SELECT DISTINCT Category FROM #products) s ON s.Category = c.Name GROUP BY c.Name HAVING COUNT(*) > 1)
        THROW 50003, 'Danh muc trung ten; vui long xu ly truoc.', 1;
    IF EXISTS (SELECT v.Brand,v.Model,v.Variant,v.Market FROM dbo.VehicleModels v JOIN #vehicles s
        ON s.Brand=v.Brand AND s.Model=v.Model AND s.Variant=v.Variant AND s.Market=v.Market
        GROUP BY v.Brand,v.Model,v.Variant,v.Market HAVING COUNT(*) > 1)
        THROW 50004, 'Dong xe trung thong tin; vui long xu ly truoc.', 1;

    INSERT dbo.Categories (Name)
    SELECT DISTINCT s.Category FROM #products s WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories c WHERE c.Name=s.Category);
    INSERT dbo.VehicleModels (Brand,Model,Variant,Market,Aliases)
    SELECT s.Brand,s.Model,s.Variant,s.Market,s.Aliases FROM #vehicles s
    WHERE NOT EXISTS (SELECT 1 FROM dbo.VehicleModels v WHERE v.Brand=s.Brand AND v.Model=s.Model AND v.Variant=s.Variant AND v.Market=s.Market);

    DECLARE @inserted TABLE (SeedKey nvarchar(120) PRIMARY KEY);
    INSERT dbo.Products (Name,Brand,Quantity,Date_Import,Price,Sold,CategoryId,Description,ImageUrl,OldPrice,
        FitmentKind,IsSampleData,ManufacturerCode,RecommendationSeedKey,RecommendationTags,RecommendationUseCases,ReferenceUrl)
    OUTPUT inserted.RecommendationSeedKey INTO @inserted(SeedKey)
    SELECT s.Name,s.Brand,s.Quantity,s.Date_Import,s.Price,s.Sold,c.Id,s.Description,s.ImageUrl,s.OldPrice,
        s.FitmentKind,s.IsSampleData,s.ManufacturerCode,s.RecommendationSeedKey,s.RecommendationTags,s.RecommendationUseCases,s.ReferenceUrl
    FROM #products s JOIN dbo.Categories c ON c.Name=s.Category
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Products p WHERE p.RecommendationSeedKey=s.RecommendationSeedKey);

    INSERT dbo.ProductFitments (ProductId,VehicleModelId,YearFrom,YearTo,EvidenceUrl,Notes)
    SELECT p.Id,v.Id,s.YearFrom,s.YearTo,s.EvidenceUrl,s.Notes FROM #fitments s
    JOIN @inserted i ON i.SeedKey=s.RecommendationSeedKey
    JOIN dbo.Products p ON p.RecommendationSeedKey=s.RecommendationSeedKey
    JOIN dbo.VehicleModels v ON v.Brand=s.Brand AND v.Model=s.Model AND v.Variant=s.Variant AND v.Market=s.Market;
    DECLARE @insertedFitments int = @@ROWCOUNT;
    IF @insertedFitments <> (SELECT COUNT(*) FROM #fitments f JOIN @inserted i ON i.SeedKey=f.RecommendationSeedKey)
        THROW 50005, 'Khong the anh xa day du thong tin tuong thich; da huy.', 1;
    DECLARE @count int = (SELECT COUNT(*) FROM @inserted);
    COMMIT TRANSACTION;
    PRINT CONCAT('Da them ', @count, ' san pham; da bo qua ', 40-@count, ' san pham co seed key.');
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
