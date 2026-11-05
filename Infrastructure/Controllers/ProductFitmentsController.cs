using Infrastructure.SqlServer.DataContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
public sealed class ProductFitmentsController(ShopContext db) : Controller
{
    [HttpGet("/admin/products/{productId:int}/fitments")]
    public async Task<IActionResult> Index(int productId)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == productId);
        if (product is null) return NotFound();
        ViewData["ProductId"] = productId;
        ViewData["ProductName"] = product.Name;
        ViewData["Vehicles"] = await db.VehicleModels.AsNoTracking().OrderBy(v => v.Brand).ThenBy(v => v.Model).ToListAsync();
        return View(await db.ProductFitments.AsNoTracking().Where(f => f.ProductId == productId).OrderBy(f => f.Id).ToListAsync());
    }

    [HttpPost("/admin/products/{productId:int}/fitments")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int productId, int id, int vehicleModelId, int yearFrom, int yearTo, string evidenceUrl, string? notes)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId)) return NotFound();
        if (!await db.VehicleModels.AnyAsync(v => v.Id == vehicleModelId) || yearFrom < 1900 || yearTo > 2100 || yearFrom > yearTo ||
            !Uri.TryCreate(evidenceUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) || evidenceUrl.Length > 2048 || notes?.Length > 1000)
        {
            TempData["Notify"] = "Thông tin chưa hợp lệ. Hãy chọn xe, khoảng đời và nguồn HTTPS chính xác.";
            return RedirectToAction(nameof(Index), new { productId });
        }
        var row = id == 0 ? new ProductFitment { ProductId = productId } : await db.ProductFitments.SingleOrDefaultAsync(f => f.Id == id && f.ProductId == productId);
        if (row is null) return NotFound();
        if (await db.ProductFitments.AnyAsync(f => f.Id != id && f.ProductId == productId && f.VehicleModelId == vehicleModelId && f.YearFrom == yearFrom && f.YearTo == yearTo))
        {
            TempData["Notify"] = "Khoảng đời xe này đã tồn tại.";
            return RedirectToAction(nameof(Index), new { productId });
        }
        row.VehicleModelId = vehicleModelId; row.YearFrom = yearFrom; row.YearTo = yearTo; row.EvidenceUrl = evidenceUrl; row.Notes = notes;
        if (id == 0) db.ProductFitments.Add(row);
        await db.SaveChangesAsync();
        TempData["Notify"] = "Đã lưu thông tin tương thích. Hãy chọn trạng thái đã xác minh trên sản phẩm nếu phù hợp.";
        return RedirectToAction(nameof(Index), new { productId });
    }

    [HttpPost("/admin/products/{productId:int}/fitments/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int productId, int id)
    {
        var row = await db.ProductFitments.SingleOrDefaultAsync(f => f.Id == id && f.ProductId == productId);
        if (row is null) return NotFound();
        db.ProductFitments.Remove(row);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { productId });
    }

    [HttpPost("/admin/products/{productId:int}/vehicles")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddVehicle(int productId, string brand, string model, string? variant, string market, string? aliases)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId)) return NotFound();
        if (string.IsNullOrWhiteSpace(brand) || brand.Length > 50 || string.IsNullOrWhiteSpace(model) || model.Length > 100 ||
            string.IsNullOrWhiteSpace(market) || market.Length > 20 || variant?.Length > 100 || aliases?.Length > 500)
        { TempData["Notify"] = "Vui lòng nhập hãng, dòng và thị trường hợp lệ."; return RedirectToAction(nameof(Index), new { productId }); }
        db.VehicleModels.Add(new VehicleModel { Brand = brand.Trim(), Model = model.Trim(), Variant = variant?.Trim() ?? "", Market = market.Trim(), Aliases = aliases?.Trim() ?? "" });
        await db.SaveChangesAsync();
        TempData["Notify"] = "Đã thêm dòng xe. Hãy thêm khoảng đời có nguồn xác nhận.";
        return RedirectToAction(nameof(Index), new { productId });
    }
}
