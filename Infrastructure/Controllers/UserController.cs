using Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Text.RegularExpressions;
using UseCase.Business_Logic;
using UseCase.UnitOfWork;

namespace Infrastructure.Controllers
{
    public class UserController : Controller
    {
        private readonly IUserManage _userManage;
        private readonly IReviewerFinder _reviewerFinder;
        private readonly IPostManage _postMange;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public UserController(IUserManage userManage, IReviewerFinder reviewerFinder, IPostManage postMange, IWebHostEnvironment webHostEnvironment)
        {
            _userManage = userManage;
            _reviewerFinder = reviewerFinder;
            _postMange = postMange;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpPost]
        public async Task<IActionResult> Comment(int rating, string comment, int productId)
        {
            var userId = HttpContext.User.FindFirst(ClaimTypes.Sid)?.Value;
            TempData["active"] = "active";
            await _reviewerFinder.AddReviewAsync(Convert.ToInt32(userId), productId, rating, comment);
            return RedirectToAction("Detail", "Product", new { id = productId });
        }


        [HttpGet("/Posts")]
        [EnableRateLimiting("Concurrency")]
        public async Task<IActionResult> PostView([FromQuery] string keyword)
        {
            var posts = await _postMange.GetPostsAsync();
            if (!string.IsNullOrEmpty(keyword))
            {
                posts = await _postMange.GetPostsAsync(keyword);
            }
            var model = new PostModel
            {
                Posts = posts.ToList()
            };
            return View(model);
        }


        [HttpGet("/Post/{id}")]
        [EnableRateLimiting("Concurrency")]
        public async Task<IActionResult> PostDetail(string id)
        {
            var post = await _postMange.GetPostDetailAsync(Guid.Parse(id));
            var model = new PostModel
            {
                Title = post.Title,
                Content = post.Content,
                Image = post.ImageUrl,
                CreatedAt = post.CreatedAt,
                Author = (await _userManage.GetUserAsync(post.UserId)).DisplayName

            };
            return View(model);
        }

        #region Manager

        [Authorize(Roles = "Admin")]
        [HttpGet("/Article")]
        [HttpGet("/Article/{id}")]
        public async Task<IActionResult> Blog(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                var post = await _postMange.GetPostDetailAsync(Guid.Parse(id));
                var model = new PostModel
                {
                    Title = post.Title,
                    Content = post.Content,
                    Image = post.ImageUrl
                };
                return View(model);
            }
            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("/Article")]
        [HttpPost("/Article/{id}")]
        public async Task<IActionResult> Blog(PostModel model, string id)
        {
            var userId = HttpContext.User.FindFirstValue(ClaimTypes.Sid);

            if (model.ImageUrl != null && model.ImageUrl.Length > 0)
            {
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "posts");
                Directory.CreateDirectory(uploadsFolder);

                var ext = Path.GetExtension(model.ImageUrl.FileName);

                // Sinh GUID không dấu gạch ngang và thêm timestamp
                var guidPart = Guid.NewGuid().ToString("N");
                var timePart = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var safeName = $"{guidPart}_{timePart}{ext}";

                var fullPath = Path.Combine(uploadsFolder, safeName);
                using var stream = new FileStream(fullPath, FileMode.Create);
                await model.ImageUrl.CopyToAsync(stream);

                model.Image = safeName; // Cập nhật tên ảnh trong mô hình
            }

            if (!string.IsNullOrEmpty(id))
            {
                await _postMange.UpdatePostAsync(new Entities.Post
                {
                    Id = Guid.Parse(id),
                    UserId = Convert.ToInt32(userId),
                    Title = model.Title,
                    Content = model.Content,
                    ImageUrl = model.Image
                });
                TempData["submit"] = "Cập nhật bài viết thành công!";
            }
            else
            {
                await _postMange.AddPostAsync(Convert.ToInt32(userId), model.Title, model.Content, model.Image);
                TempData["submit"] = "Đăng bài viết thành công!";
            }
            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("/BlogList")]
        public async Task<IActionResult> BlogManage()
        {
            var posts = await _postMange.GetPostsAsync();
            var model = new PostModel
            {
                Posts = posts.ToList()
            };
            return View(model);
        }

        [HttpGet("/Delete/{id}")]
        public async Task<IActionResult> DeletePost(string id)
        {
            var uploadsPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "posts");
            try
            {
                await _postMange.DeletePostAsync(Guid.Parse(id), uploadsPath);
                TempData["delete"] = "Xóa bài viết thành công!";
            }
            catch (Exception ex)
            {
                TempData["delete"] = $"Xóa bài viết thất bại: {ex.Message}";
            }
            return RedirectToAction("BlogManage");
        }

        #endregion
    }
}
