using System.Globalization;
using System.Net;
using System.Text;
using Infrastructure.Models;

namespace Infrastructure.PaymentSupport;

public static class CodEmailContent
{
    public static string Create(CodCheckoutModel contact, IReadOnlyList<CartModel> items, double total, string brand, bool forStore)
    {
        static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");
        var body = new StringBuilder("<!doctype html><html lang=\"vi\"><meta charset=\"utf-8\"><body style=\"font-family:Arial,sans-serif;color:#171717\">");
        body.Append($"<h1>{Encode(brand)} — {(forStore ? "Yêu cầu đặt hàng COD" : "Xác nhận đặt hàng COD")}</h1>");
        body.Append("<p><strong>Phương thức: COD — Thanh toán khi nhận hàng.</strong></p><p>Trạng thái thanh toán: <strong>Chưa thanh toán</strong>. Thanh toán cho đơn vị giao hàng khi nhận sản phẩm.</p>");
        body.Append($"<p>Khách hàng: {Encode(contact.Name)}<br>Số điện thoại: {Encode(contact.Phone)}<br>Email: {Encode(contact.Email)}<br>Địa chỉ nhận hàng: {Encode(contact.Address)}</p>");
        body.Append("<h2>Sản phẩm</h2><ul>");
        foreach (var item in items)
            body.Append($"<li>{Encode(item.Product.Name)} × {item.Quantity}: {(item.Product.Price * item.Quantity).ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} ₫</li>");
        body.Append($"</ul><p><strong>Tổng tiền sản phẩm: {total.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} ₫</strong></p>");
        if (!string.IsNullOrWhiteSpace(contact.Note)) body.Append($"<p>Ghi chú: {Encode(contact.Note).Replace("\n", "<br>")}</p>");
        body.Append("<p>Cửa hàng sẽ liên hệ để xác nhận thông tin nhận hàng và chi phí giao hàng nếu có.</p></body></html>");
        return body.ToString();
    }
}
