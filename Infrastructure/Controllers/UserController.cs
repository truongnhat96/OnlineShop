using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UseCase.Business_Logic;

namespace Infrastructure.Controllers
{
    public class UserController : Controller
    {
        private readonly IUserManage _userManage;

        public UserController(IUserManage userManage)
        {
            _userManage = userManage;
        }

        [HttpPost]
        public async Task<IActionResult> Comment(int rating, string comment)
        {
            var userId = HttpContext.User.FindFirst(ClaimTypes.Sid)?.Value;
            TempData["active"] = "active";
            int productId = Convert.ToInt32(TempData["productId"]?.ToString());
            await _userManage.AddReviewAsync(Convert.ToInt32(userId), productId, rating, comment);
            return Redirect(Url.RouteUrl("product", new { id = productId, oldPrice = Convert.ToDouble(TempData["oldPrice"]) }) ?? throw new("Url null"));
        }

        [Authorize(Roles = "Manager")]
        [HttpGet("/Article")]
        public IActionResult Blog()
        {
            return View();
        }
    }
}
