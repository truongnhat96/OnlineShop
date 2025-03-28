using Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using UseCase.Business_Logic;

namespace Infrastructure.Controllers
{
    public class CartController : Controller
    {
        public const string CartSessionKey = "Cart";
        public const string CouponSessionKey = "CouponKey";
        private readonly ICartManage _cartManage;
        private readonly ILogger<CartController> _logger;

        public CartController(ICartManage cartManage, ILogger<CartController> logger)
        {
            _cartManage = cartManage;
            _logger = logger;
        }

        [HttpGet("/Cart")]
        public async Task<IActionResult> CartView()
        {
            var user = HttpContext.User;
            var model = new List<CartModel>();
            if (user.Identity != null && user.Identity.IsAuthenticated)
            {
                var userId = int.Parse(user.FindFirstValue(ClaimTypes.Sid) ?? "0");
                var cartItems = await _cartManage.GetCartItemsAsync(userId);
                foreach (var item in cartItems)
                {
                    model.Add(new CartModel
                    {
                        Quantity = item.Quantity,
                        Product = await _cartManage.GetProductInCartAsync(item.ProductId)
                    });
                }
            }
            else
            {
                var cartSTR = HttpContext.Session.GetString(CartSessionKey);
                if (!string.IsNullOrEmpty(cartSTR))
                {
                    model = JsonSerializer.Deserialize<List<CartModel>>(cartSTR);
                }
            }
            TempData["CartMessage"] = TempData["CartMessage-Temp"];
            return View(model);
        }


        [HttpGet("/Cart-Add/{productId}/{productName}", Name = "cart")]
        public async Task<IActionResult> Add(int productId, string productName, [FromQuery]int quantity)
        {
            var user = HttpContext.User;
            if (user.Identity != null && user.Identity.IsAuthenticated)
            {
                var userId = int.Parse(user.FindFirstValue(ClaimTypes.Sid) ?? "0");
                await _cartManage.AddCartItemAsync(productId, userId, quantity);
            }
            else
            {
                var cartSTR = HttpContext.Session.GetString(CartSessionKey);
                if (!string.IsNullOrEmpty(cartSTR))
                {
                    var cart = JsonSerializer.Deserialize<List<CartModel>>(cartSTR)!;
                    var existingItem = cart.FirstOrDefault(x => x.Product.Id == productId);
                    if (existingItem != null)
                    {
                        existingItem.Quantity += quantity;
                    }
                    else
                    {
                        cart.Add(new CartModel
                        {
                            Quantity = quantity,
                            Product = await _cartManage.GetProductInCartAsync(productId)
                        });
                    }
                    HttpContext.Session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));
                }
                else
                {
                    var cart = new List<CartModel>
                    {
                        new CartModel
                        {
                            Quantity = quantity,
                            Product = await _cartManage.GetProductInCartAsync(productId)
                        }
                    };
                    HttpContext.Session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));
                }
            }
            TempData["CartMessage-Temp"] = $"\"{productName}\" đã được thêm vào giỏ hàng";
            return RedirectToAction("CartView");
        }


        [HttpGet("/Cart-Remove/{productId}/{productName}")]
        public async Task<IActionResult> Remove(int productId, string productName)
        {
            var user = HttpContext.User;
            if (user.Identity != null && user.Identity.IsAuthenticated)
            {
                var userId = int.Parse(user.FindFirstValue(ClaimTypes.Sid) ?? "0");
                await _cartManage.RemoveCartItemAsync(productId, userId);
            }
            else
            {
                var cartSTR = HttpContext.Session.GetString(CartSessionKey);
                if (!string.IsNullOrEmpty(cartSTR))
                {
                    var cart = JsonSerializer.Deserialize<List<CartModel>>(cartSTR)!;
                    var existingItem = cart.FirstOrDefault(x => x.Product.Id == productId);
                    if (existingItem != null)
                    {
                        cart.Remove(existingItem);
                    }
                    HttpContext.Session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));
                }
            }
            TempData["CartMessage-Temp"] = $"Đã xóa \"{productName}\" khỏi giỏ hàng";
            return RedirectToAction("CartView");
        }


        [HttpPost("/Cart-Update")]
        public async Task<IActionResult> UpdateCart([FromBody]CartUpdateRequest model)
        {
            var user = HttpContext.User;
            bool isDeleted = false;
            if (user.Identity != null && user.Identity.IsAuthenticated)
            {
                foreach (var item in model.UpdateItem)
                {
                    var userId = int.Parse(user.FindFirstValue(ClaimTypes.Sid) ?? "0");
                    if (item.Quantity == 0)
                    {
                        await _cartManage.RemoveCartItemAsync(item.ProductId, userId);
                        isDeleted = true;
                    }
                    else
                    {
                        await _cartManage.UpdateCartItemAsync(item.ProductId, userId, item.Quantity);
                    }
                }
            }
            else
            {
                foreach (var item in model.UpdateItem)
                {
                    var cartSTR = HttpContext.Session.GetString(CartSessionKey)!;
                    var cart = JsonSerializer.Deserialize<List<CartModel>>(cartSTR)!;
                    var existingItem = cart.FirstOrDefault(x => x.Product.Id == item.ProductId);
                    if (existingItem != null)
                    {
                        if (item.Quantity == 0)
                        {
                            cart.Remove(existingItem);
                        }
                        else
                        {
                            existingItem.Quantity = item.Quantity;
                        }
                    }
                    HttpContext.Session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));
                }
            }
            return Json(new { success = true, reload = isDeleted });
        }


        [Authorize]
        [HttpPost("/Discount")]
        public async Task<IActionResult> Discount(string coupon)
        {
            var couponEntered = new List<CouponModel>();
            var couponSTR = HttpContext.Session.GetString(CouponSessionKey);
            if (!string.IsNullOrEmpty(couponSTR))
            {
                couponEntered = JsonSerializer.Deserialize<List<CouponModel>>(couponSTR)!;
            }
            if (int.TryParse(User.FindFirstValue(ClaimTypes.Sid), out int userId))
            {
                var cartItems = await _cartManage.GetCartItemsAsync(userId);
                var model = new List<CartModel>();
                bool isDiscount = false;
                foreach (var item in cartItems)
                {
                    dynamic product;
                    try
                    {
                        product = await _cartManage.GetProductInCartAfterDiscountAsync(item.ProductId, coupon);
                        isDiscount = await _cartManage.IsCouponUsed(userId, item.ProductId);
                        if (couponEntered.FirstOrDefault(x => x.Id == item.ProductId) == null)
                        {
                            couponEntered.Add(new CouponModel
                            {
                                Id = item.ProductId,
                                Name = coupon
                            });
                        }
                    }
                    catch
                    {
                        product = await _cartManage.GetProductInCartAsync(item.ProductId);
                    }
                    model.Add(new CartModel
                    {
                        Quantity = item.Quantity,
                        Product = product
                    });
                }
                HttpContext.Session.SetString(CouponSessionKey, JsonSerializer.Serialize(couponEntered));
                if (!isDiscount)
                {
                    TempData["CartError"] = "Mã giảm giá không tồn tại hoặc đã được sử dụng";
                }
                else
                {
                    TempData["CartMessage"] = "Mã đã được áp dụng thành công - Lưu ý: Mã chỉ được áp dụng một lần khi mua hàng";
                }
                return View("CartView", model);
            }
            return RedirectToAction("CartView");
        }


    }
}
