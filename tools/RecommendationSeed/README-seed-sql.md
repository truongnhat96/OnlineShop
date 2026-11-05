# Chuyển dữ liệu sản phẩm sang máy khác

`seed-40-products.sql` chứa snapshot 40 sản phẩm seed từ database nguồn lúc xuất, cùng danh mục, dòng xe và tương thích liên quan. Không bao gồm tài khoản, đơn hàng, đánh giá hoặc coupon. Giá/tồn kho có thể là dữ liệu kiểm thử; kiểm tra cờ `IsSampleData` và cập nhật theo thực tế trước khi bán hàng.

## Chạy trên máy đích

1. Sao chép dự án và tệp SQL sang máy đích. Dự án trên máy đích cần có migration `20261104110652_MotorcycleRecommendations`.
2. Tạo/chọn database SQL Server đích và áp dụng migration của dự án. Nếu đang đứng ở thư mục gốc dự án, ví dụ với Windows Authentication:

   ```powershell
   dotnet ef database update --project Infrastructure.SqlServer/Infrastructure.SqlServer.csproj --startup-project Infrastructure.SqlServer/Infrastructure.SqlServer.csproj --context ShopContext --connection "Server=.\SQLEXPRESS;Database=OnlineShop;Trusted_Connection=True;TrustServerCertificate=True;"
   ```

   Đổi server/database theo máy đích. Máy cần có `dotnet-ef`; SQL Server phải hỗ trợ `OPENJSON` (SQL Server 2016 trở lên, database compatibility level tối thiểu 130).
3. Trong SQL Server Management Studio, mở `seed-40-products.sql`, chọn database đích trên thanh công cụ và Execute. Hoặc chạy từ thư mục gốc dự án:

   ```powershell
   sqlcmd -S ".\SQLEXPRESS" -d OnlineShop -E -C -f 65001 -b -i "tools\RecommendationSeed\seed-40-products.sql"
   ```

4. Sao chép ảnh minh họa `Infrastructure/wwwroot/uploads/products/motoshop-seed-illustration.svg` sang cùng đường dẫn trong ứng dụng đích. Nếu sản phẩm đã được thay ảnh trên máy nguồn, sao chép thêm từng tệp ảnh tương ứng trong `Infrastructure/wwwroot/uploads/products/`; SQL chỉ lưu tên ảnh, không chứa nội dung tệp ảnh.

Script tạo danh mục còn thiếu theo tên, ánh xạ xe theo hãng/dòng/phiên bản/thị trường và thêm tương thích cho sản phẩm mới. Chạy lại bỏ qua sản phẩm có cùng `RecommendationSeedKey`, không cập nhật dữ liệu đã có. Nếu database có nhiều danh mục cùng tên hoặc nhiều dòng xe có cùng định danh, script báo lỗi và rollback.

## Xuất lại khi dữ liệu nguồn thay đổi

Từ máy nguồn có quyền đọc SQL Server, chạy:

```powershell
powershell -NoProfile -File tools/RecommendationSeed/export-40-products.ps1
```

Mặc định công cụ đọc `OnlineShop` trên `.\SQLEXPRESS` và ghi đè `tools/RecommendationSeed/seed-40-products.sql`. Có thể truyền `-Server`, `-Database` và `-OutputFile`. Công cụ yêu cầu đúng 40 bản ghi có seed key `motoshop-v1-*`; không xuất dữ liệu sản phẩm khác.

## Đã kiểm tra

Ngày 05/11/2026: đã nhập vào SQL Server database thử nghiệm `OnlineShop_SeedExport_Test`: 40 sản phẩm, 6 danh mục, 6 dòng xe và 15 tương thích. Chạy lần hai thêm 0 sản phẩm. Đối chiếu toàn bộ trường sản phẩm và tương thích với database nguồn: 0 sai khác. Database thử nghiệm được giữ lại, không tự xóa.
