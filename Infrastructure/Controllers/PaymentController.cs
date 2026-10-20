using Infrastructure.Models;
using Infrastructure.Models.VnPay;
using Infrastructure.PaymentSupport.Momo.Service;
using Infrastructure.PaymentSupport.VnPay.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using UseCase.Business_Logic;
using UseCase.MailService;
using Infrastructure.PaymentSupport;

namespace Infrastructure.Controllers
{
    public class PaymentController : Controller
    {
        public const string adminEmail = "luongnhattruong2004@gmail.com";
        private readonly IVnPayService _vnPayService;
        private readonly IMomoService _momoService;
        private readonly ICartManage _cartManage;
        private readonly IMailSender _mailSender;
        private readonly ILogger<PaymentController>? _logger;
        private readonly StoreBranding _brand;
        public const string CodReceiptSessionKey = "CodReceipt";

        public PaymentController(IVnPayService vnPayService, IMomoService momoService, ICartManage cartManage, IMailSender mailSender, ILogger<PaymentController>? logger = null, IConfiguration? configuration = null)
        {
            _vnPayService = vnPayService;
            _momoService = momoService;
            _cartManage = cartManage;
            _mailSender = mailSender;
            _logger = logger;
            _brand = configuration is null ? new StoreBranding() : StoreBranding.From(configuration);
        }

        [HttpPost("/Payment/PayByCod")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayByCod(CodCheckoutModel model)
        {
            var userId = int.TryParse(User.FindFirstValue(ClaimTypes.Sid), out var id) ? id : 0;
            List<CartModel> items;
            try { items = await LoadCheckoutItemsAsync(userId); }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "Could not load COD cart. TraceId={TraceId}", HttpContext.TraceIdentifier);
                TempData["CartError"] = "Không thể kiểm tra giỏ hàng. Vui lòng kiểm tra lại sản phẩm và mã giảm giá.";
                return RedirectToAction("CartView", "Cart");
            }
            if (items.Count == 0) return RedirectToAction("CartView", "Cart");
            if (items.Any(item => item.Product == null))
            {
                TempData["CartError"] = "Có sản phẩm không còn bán. Vui lòng kiểm tra lại giỏ hàng.";
                return RedirectToAction("CartView", "Cart");
            }
            ViewData["CodCheckout"] = model;
            if (!ModelState.IsValid) return View("Option", items);
            foreach (var item in items)
            {
                if (item.Quantity <= 0 || item.Quantity > item.Product.Quantity || !double.IsFinite(item.Product.Price) || item.Product.Price < 0)
                    ModelState.AddModelError(string.Empty, "Số lượng hoặc giá sản phẩm đã thay đổi. Vui lòng kiểm tra lại giỏ hàng.");
            }
            foreach (var group in items.GroupBy(item => item.Product.Id))
                if (group.Sum(item => (long)item.Quantity) > group.First().Product.Quantity)
                    ModelState.AddModelError(string.Empty, "Số lượng sản phẩm vượt quá tồn kho. Vui lòng kiểm tra lại giỏ hàng.");
            var total = items.Sum(item => item.Product.Price * item.Quantity);
            if (!double.IsFinite(total)) ModelState.AddModelError(string.Empty, "Tổng tiền không hợp lệ. Vui lòng kiểm tra lại giỏ hàng.");
            if (!ModelState.IsValid) return View("Option", items);

            try
            {
                // Use the existing SMTP sender for both recipients. A rejected send must not
                // silently confirm COD or discard the customer's cart.
                var customerSent = await _mailSender.SendMailAsync(new MailContent {
                    To = model.Email, Subject = $"{_brand.Name} — XÁC NHẬN ĐẶT HÀNG COD",
                    Body = CodEmailContent.Create(model, items, total, _brand.Name, false)
                });
                var storeSent = await _mailSender.SendMailAsync(new MailContent {
                    To = adminEmail, Subject = $"{_brand.Name} — YÊU CẦU ĐẶT HÀNG COD",
                    Body = CodEmailContent.Create(model, items, total, _brand.Name, true)
                });
                if (!customerSent || !storeSent)
                {
                    _logger?.LogWarning("COD email rejected. CustomerAccepted={CustomerAccepted} StoreAccepted={StoreAccepted} TraceId={TraceId}", customerSent, storeSent, HttpContext.TraceIdentifier);
                    ModelState.AddModelError(string.Empty, "Chưa gửi được đầy đủ email xác nhận COD. Giỏ hàng của bạn vẫn được giữ. Vui lòng thử lại hoặc liên hệ cửa hàng.");
                    return View("Option", items);
                }
                if (userId != 0)
                {
                    var coupons = ReadCoupons();
                    var applied = coupons.Where(coupon => items.Any(item => item.Product.Id == coupon.Id)).Select(coupon => coupon.Id).Distinct().ToList();
                    if (applied.Count == 0) await _cartManage.OrderProcessingAsync(userId, 0);
                    else foreach (var couponId in applied) await _cartManage.OrderProcessingAsync(userId, couponId);
                }
                else
                {
                    await _cartManage.OrderProcessingGuestAsync(items.Select(item => new Entities.CartItem { ProductId = item.Product.Id, Quantity = item.Quantity }));
                    HttpContext.Session.Remove(CartController.CartSessionKey);
                }
                HttpContext.Session.Remove(CartController.CouponSessionKey);
                HttpContext.Session.SetString(CodReceiptSessionKey, JsonSerializer.Serialize(new CodConfirmationModel { Total = total, Email = model.Email }));
                return RedirectToAction(nameof(CodConfirmed));
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "COD confirmation failed. TraceId={TraceId}", HttpContext.TraceIdentifier);
                ModelState.AddModelError(string.Empty, "Chưa hoàn tất yêu cầu COD. Vui lòng liên hệ cửa hàng để đối chiếu trước khi gửi lại.");
                return View("Option", items);
            }
        }

        [HttpGet("/Payment/CodConfirmed")]
        public IActionResult CodConfirmed()
        {
            var receipt = HttpContext.Session.GetString(CodReceiptSessionKey);
            if (string.IsNullOrEmpty(receipt)) return RedirectToAction("CartView", "Cart");
            return View(JsonSerializer.Deserialize<CodConfirmationModel>(receipt));
        }

        private List<CouponModel> ReadCoupons()
        {
            var json = HttpContext.Session.GetString(CartController.CouponSessionKey);
            return string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<CouponModel>>(json) ?? [];
        }

        [HttpPost("/CF-Order")]
        public async Task<IActionResult> Order(string name, string phone, string email, string? note, double totalAmount)
        {
            int userId = int.Parse(HttpContext.User.FindFirstValue(ClaimTypes.Sid) ?? "0");
            var couponSTR = HttpContext.Session.GetString(CartController.CouponSessionKey);
            if (userId != 0)
            {
                var cartItems = await _cartManage.GetCartItemsAsync(userId);
                List<string> productNames = new();
                foreach (var item in cartItems)
                {
                    var productName = (await _cartManage.GetProductInCartAsync(item.ProductId)).Name;
                    productNames.Add(productName + $" x {item.Quantity}");
                }

                if (!string.IsNullOrEmpty(couponSTR) && JsonSerializer.Deserialize<List<CouponModel>>(couponSTR)!.Count > 0)
                {
                    var couponEntered = JsonSerializer.Deserialize<List<CouponModel>>(couponSTR)!;
                    foreach (var item in couponEntered)
                    {
                        await _cartManage.OrderProcessingAsync(userId, item.Id);
                    }
                }
                else
                {
                    await _cartManage.OrderProcessingAsync(userId, 0);
                }

                await _mailSender.SendMailAsync(new MailContent
                {
                    To = email,
                    Subject = "XÁC NHẬN ĐƠN HÀNG",
                    Body = HtmlHelper.GenerateHTMLContent(productNames, false)
                });

                await _mailSender.SendMailAsync(new MailContent
                {
                    To = adminEmail,
                    Subject = "ĐƠN HÀNG MỚI",
                    Body = HtmlHelper.GenerateHTMLContent(productNames, name, phone, email, note)
                });
            }
            else
            {
                var cartSTR = HttpContext.Session.GetString(CartController.CartSessionKey);
                if (!string.IsNullOrEmpty(cartSTR))
                {
                    var cart = JsonSerializer.Deserialize<List<CartModel>>(cartSTR)!;
                    List<string> productNames = new();
                    foreach (var item in cart)
                    {
                        productNames.Add(item.Product.Name + $" x {item.Quantity}");
                    }
                    HttpContext.Session.Remove(CartController.CartSessionKey);
                    await _mailSender.SendMailAsync(new MailContent
                    {
                        To = email,
                        Subject = "XÁC NHẬN ĐƠN HÀNG",
                        Body = HtmlHelper.GenerateHTMLContent(productNames, false)
                    });

                    await _mailSender.SendMailAsync(new MailContent
                    {
                        To = adminEmail,
                        Subject = "ĐƠN HÀNG MỚI",
                        Body = HtmlHelper.GenerateHTMLContent(productNames, name, phone, email, note)
                    });
                }
            }
                return View(totalAmount);
        }

        [HttpGet("/Pay/{userId}")]
        public async Task<IActionResult> Option(int userId)
        {
            return View(await LoadCheckoutItemsAsync(userId));
        }

        private async Task<List<CartModel>> LoadCheckoutItemsAsync(int userId)
        {
            var model = new List<CartModel>();
            if (userId != 0)
            {
                var cartItems = await _cartManage.GetCartItemsAsync(userId);
                var couponSTR = HttpContext.Session.GetString(CartController.CouponSessionKey);
                if (!string.IsNullOrEmpty(couponSTR))
                {
                    var couponEntered = JsonSerializer.Deserialize<List<CouponModel>>(couponSTR)!;
                    foreach (var item in cartItems)
                    {
                        if (couponEntered.Any(x => x.Id == item.ProductId))
                        {
                            var product = await _cartManage.GetProductInCartAfterDiscountAsync(item.ProductId, couponEntered.FirstOrDefault(x => x.Id == item.ProductId)!.Name);
                            model.Add(new CartModel
                            {
                                Quantity = item.Quantity,
                                Product = product
                            });
                        }
                        else
                        {
                            var product = await _cartManage.GetProductInCartAsync(item.ProductId);
                            model.Add(new CartModel
                            {
                                Quantity = item.Quantity,
                                Product = product
                            });
                        }
                    }
                }
                else // No coupon
                {
                    foreach (var item in cartItems)
                    {
                        var product = await _cartManage.GetProductInCartAsync(item.ProductId);
                        model.Add(new CartModel
                        {
                            Quantity = item.Quantity,
                            Product = product
                        });
                    }
                }
            }
            else //User not logged in
            {
                var cartSTR = HttpContext.Session.GetString(CartController.CartSessionKey);
                if (!string.IsNullOrEmpty(cartSTR))
                {
                    var cart = JsonSerializer.Deserialize<List<CartModel>>(cartSTR)!;
                    foreach (var item in cart)
                    {
                        var product = await _cartManage.GetProductInCartAsync(item.Product.Id);
                        model.Add(new CartModel
                        {
                            Quantity = item.Quantity,
                            Product = product
                        });
                    }
                }
            }
            return model;
        }

        [Authorize]
        [HttpPost]
        public IActionResult PayByVnpay(PaymentInformationModel model)
        {
            string url = _vnPayService.CreatePaymentUrl(model, HttpContext) ?? throw new("Server not response");
            return Redirect(url);
        }


        [HttpGet("/PaymentCallbackVnpay")]
        public async Task<IActionResult> PaymentCallbackVnpay()
        {
            var response = _vnPayService.PaymentExecute(Request.Query);
            if (response.Success && !response.TransactionId.Equals("0"))
            {
                var couponSTR = HttpContext.Session.GetString(CartController.CouponSessionKey);
                if (int.TryParse(HttpContext.User.FindFirstValue(ClaimTypes.Sid), out int userId))
                {
                    // Get product names in cart
                    var cartItems = await _cartManage.GetCartItemsAsync(userId);
                    List<string> productNames = new();
                    foreach (var item in cartItems)
                    {
                        var productName = (await _cartManage.GetProductInCartAsync(item.ProductId)).Name;
                        productNames.Add(productName + $" x {item.Quantity}");
                    }

                    if (!string.IsNullOrEmpty(couponSTR) && JsonSerializer.Deserialize<List<CouponModel>>(couponSTR)!.Count > 0)
                    {
                        var couponEntered = JsonSerializer.Deserialize<List<CouponModel>>(couponSTR)!;
                        foreach (var item in couponEntered)
                        {
                            await _cartManage.OrderProcessingAsync(userId, item.Id);
                        }
                    }
                    else
                    {
                        await _cartManage.OrderProcessingAsync(userId, 0);
                    }

                    await _mailSender.SendMailAsync(new MailContent
                    {
                        To = HttpContext.User.FindFirstValue(ClaimTypes.Email) ?? throw new("No Email"),
                        Subject = "THANH TOÁN THÀNH CÔNG!",
                        Body = HtmlHelper.GenerateHTMLContent(productNames, true)
                    });
                }
            }
            return View(response);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PayByMomo(OrderInfoModel model)
        {
            var response = await _momoService.CreatePaymentAsync(model) ?? throw new("No Response");
            if (string.IsNullOrEmpty(response.PayUrl))
            {
                throw new("Server not response");
            }
                return Redirect(response.PayUrl);
        }

        [HttpGet("/PaymentCallbackMomo")]
        public async Task<IActionResult> PaymentCallbackMomo()
        {
            var response = _momoService.PaymentExecute(Request.Query);
            var requestQuery = Request.Query;
            if (requestQuery["errorCode"] == "0")
            {
                TempData["CODE"] = requestQuery["transId"];
                var couponSTR = HttpContext.Session.GetString(CartController.CouponSessionKey);
                if (int.TryParse(HttpContext.User.FindFirstValue(ClaimTypes.Sid), out int userId))
                {
                    // Get product names
                    var cartItems = await _cartManage.GetCartItemsAsync(userId);
                    List<string> productNames = new();
                    foreach (var item in cartItems)
                    {
                        var productName = (await _cartManage.GetProductInCartAsync(item.ProductId)).Name;
                        productNames.Add(productName + $" x {item.Quantity}");
                    }

                    if (!string.IsNullOrEmpty(couponSTR) && JsonSerializer.Deserialize<List<CouponModel>>(couponSTR)!.Count > 0)
                    {
                        var couponEntered = JsonSerializer.Deserialize<List<CouponModel>>(couponSTR)!;
                        foreach (var item in couponEntered)
                        {
                            await _cartManage.OrderProcessingAsync(userId, item.Id);
                        }
                    }
                    else
                    {
                        await _cartManage.OrderProcessingAsync(userId, 0);
                    }

                    await _mailSender.SendMailAsync(new MailContent
                    {
                        To = HttpContext.User.FindFirstValue(ClaimTypes.Email) ?? throw new("No Email"),
                        Subject = "THANH TOÁN THÀNH CÔNG!",
                        Body = HtmlHelper.GenerateHTMLContent(productNames, true)
                    });
                }
            }
            return View(response);
        }
    }
}
