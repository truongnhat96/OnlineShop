SET XACT_ABORT ON;
IF DB_NAME() <> N'OnlineShop_Recommendation_Test' THROW 50000, 'Wrong test database', 1;
INSERT INTO dbo.Categories (Name) VALUES
 (N'Phụ kiện tiện ích'), (N'Tay lái & Gương'), (N'Bảo vệ xe'),
 (N'Điện & chiếu sáng'), (N'Chăm sóc & bảo dưỡng'), (N'Đồ bảo hộ');
