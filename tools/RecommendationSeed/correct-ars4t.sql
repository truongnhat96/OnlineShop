SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
IF DB_NAME() <> N'OnlineShop' THROW 50000, 'Unexpected database', 1;
BEGIN TRANSACTION;
UPDATE dbo.Products
SET Name = N'Quần mưa GIVI ARS4T Acquaride', RecommendationTags = N'quần mưa,đồ đi mưa',
    ReferenceUrl = N'https://www.dobaohomoto.vn/san-pham/chi-tiet/quan-mua-givi-ars4t-12061'
WHERE ManufacturerCode = N'ARS4T' AND IsSampleData = 1 AND RecommendationSeedKey LIKE N'motoshop-v1-%'
  AND Name = N'Bộ đồ mưa GIVI ARS4T Acquaride' AND RecommendationTags = N'áo mưa,quần áo mưa'
  AND ReferenceUrl = N'https://givi.com.vn/vi/do-di-mua';
IF @@ROWCOUNT <> 1 THROW 50001, 'Original seed row was changed; no update applied', 1;
COMMIT TRANSACTION;
