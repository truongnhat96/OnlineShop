using Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UseCase.Business_Logic;
using UseCase.UnitOfWork;

namespace Infrastructure.Controllers
{
    public class UserController : Controller
    {
        private readonly IUserManage _userManage;
        private readonly IReviewerFinder _reviewerFinder;
        private readonly IPostManage _postMange;

        public UserController(IUserManage userManage, IReviewerFinder reviewerFinder, IPostManage postMange)
        {
            _userManage = userManage;
            _reviewerFinder = reviewerFinder;
            _postMange = postMange;
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
        public async Task<IActionResult> PostView([FromQuery]string keyword)
        {
            var posts = await _postMange.GetPostsAsync();
            if(!string.IsNullOrEmpty(keyword))
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

        [Authorize(Roles = "Manager")]
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

        [Authorize(Roles = "Manager")]
        [HttpPost("/Article")]
        [HttpPost("/Article/{id}")]
        public async Task<IActionResult> Blog(PostModel model, string id)
        {
            var userId = HttpContext.User.FindFirstValue(ClaimTypes.Sid);

            if(model.ImageUrl != null)
            {
                var imageFile = model.ImageUrl.FileName;
                if (!System.IO.File.Exists(imageFile))
                {
                    var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/posts", imageFile);
                    using (var stream = new FileStream(path, FileMode.Create))
                    {
                        await model.ImageUrl.CopyToAsync(stream);
                    }
                }

                if (!string.IsNullOrEmpty(id))
                {
                    await _postMange.UpdatePostAsync(new Entities.Post 
                    {
                        Id = Guid.Parse(id),
                        UserId = Convert.ToInt32(userId),
                        Title = model.Title,
                        Content = model.Content,
                        ImageUrl = imageFile
                    });
                    TempData["submit"] = "Cập nhật bài viết thành công!";
                }
                else
                {
                    await _postMange.AddPostAsync(Convert.ToInt32(userId), model.Title, model.Content, imageFile);
                    TempData["submit"] = "Đăng bài viết thành công!";
                }
            }
            else
            {
                throw new("Image is required");
            }
            return View();
        }

        [Authorize(Roles = "Manager")]
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
            await _postMange.DeletePostAsync(Guid.Parse(id));
            TempData["delete"] = "Xóa bài viết thành công!";
            return RedirectToAction("BlogManage");
        }

        #endregion
    }
}
