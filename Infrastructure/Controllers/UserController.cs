using Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Infrastructure.EditorImages;
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
        private readonly EditorContentService _editorContent;
        private readonly ILogger<UserController>? _logger;

        public UserController(IUserManage userManage, IReviewerFinder reviewerFinder, IPostManage postMange, IWebHostEnvironment webHostEnvironment, EditorContentService editorContent, ILogger<UserController>? logger = null)
        {
            _userManage = userManage;
            _reviewerFinder = reviewerFinder;
            _postMange = postMange;
            _webHostEnvironment = webHostEnvironment;
            _editorContent = editorContent;
            _logger = logger;
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
        public async Task<IActionResult> Blog([FromRoute] string? id)
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
        [RequestFormLimits(ValueLengthLimit = 3 * 1024 * 1024)]
        public async Task<IActionResult> Blog(PostModel model, [FromRoute] string? id)
        {
            async Task<IActionResult> InvalidForm()
            {
                ViewData["RequireCover"] = string.IsNullOrEmpty(id);
                if (Guid.TryParse(id, out var postId)) model.Image = (await _postMange.GetPostDetailAsync(postId)).ImageUrl;
                return View(model);
            }
            try { model.Content = _editorContent.PrepareForSave(model.Content); }
            catch (EditorImageException exception)
            {
                ModelState.AddModelError(nameof(model.Content), exception.Message);
                return await InvalidForm();
            }
            var userId = HttpContext.User.FindFirstValue(ClaimTypes.Sid);
            if (!int.TryParse(userId, out var authorId) || authorId <= 0) return Challenge();
            if (string.IsNullOrWhiteSpace(model.Title)) ModelState.AddModelError(nameof(model.Title), "Vui lòng nhập tiêu đề bài viết.");
            if (string.IsNullOrEmpty(id) && (model.ImageUrl == null || model.ImageUrl.Length == 0))
                ModelState.AddModelError(nameof(model.ImageUrl), "Vui lòng chọn ảnh bìa bài viết.");
            if (!ModelState.IsValid) return await InvalidForm();

            try
            {
                if (model.ImageUrl != null && model.ImageUrl.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "posts");
                    Directory.CreateDirectory(uploadsFolder);
                    var ext = Path.GetExtension(model.ImageUrl.FileName);
                    var guidPart = Guid.NewGuid().ToString("N");
                    var timePart = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    var safeName = $"{guidPart}_{timePart}{ext}";
                    var fullPath = Path.Combine(uploadsFolder, safeName);
                    using var stream = new FileStream(fullPath, FileMode.Create);
                    await model.ImageUrl.CopyToAsync(stream);
                    model.Image = safeName;
                }

                if (!string.IsNullOrEmpty(id))
                {
                    await _postMange.UpdatePostAsync(new Entities.Post
                    {
                        Id = Guid.Parse(id),
                        UserId = authorId,
                        Title = model.Title,
                        Content = model.Content,
                        ImageUrl = model.Image
                    });
                    TempData["submit"] = "Cập nhật bài viết thành công!";
                }
                else
                {
                    await _postMange.AddPostAsync(authorId, model.Title, model.Content, model.Image);
                    TempData["submit"] = "Đăng bài viết thành công!";
                }
                return RedirectToAction(nameof(BlogManage));
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Failed to save article {PostId}", id);
                ModelState.AddModelError(string.Empty, "Chưa lưu được bài viết. Vui lòng thử lại.");
                return await InvalidForm();
            }
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

        [Authorize(Roles = "Admin")]
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
