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

namespace Infrastructure.Controllers
{
    public class PaymentController : Controller
    {
        public const string adminEmail = "luongnhattruong2004@gmail.com";
        private readonly IVnPayService _vnPayService;
        private readonly IMomoService _momoService;
        private readonly ICartManage _cartManage;
        private readonly IMailSender _mailSender;

        public PaymentController(IVnPayService vnPayService, IMomoService momoService, ICartManage cartManage, IMailSender mailSender)
        {
            _vnPayService = vnPayService;
            _momoService = momoService;
            _cartManage = cartManage;
            _mailSender = mailSender;
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
            return View(model);
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
