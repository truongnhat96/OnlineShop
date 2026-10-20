using Entities;
using Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Infrastructure.EditorImages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.RegularExpressions;
using UseCase;
using UseCase.Business_Logic;
using UseCase.UnitOfWork;

namespace Infrastructure.Controllers
{
    public class ProductController : Controller
    {
        private readonly IProductManage _productManager;
        private readonly IReviewerFinder _reviewerFinder;
        private readonly ILogger<ProductController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly EditorContentService _editorContent;

        public ProductController(IProductManage productManager, ILogger<ProductController> logger, IReviewerFinder reviewerFinder, IWebHostEnvironment webHostEnvironment, EditorContentService editorContent)
        {
            _productManager = productManager;
            _logger = logger;
            _reviewerFinder = reviewerFinder;
            _webHostEnvironment = webHostEnvironment;
            _editorContent = editorContent;

            _logger.LogInformation("Environment: {EnvironmentName}", _webHostEnvironment.EnvironmentName);
        }


        #region Customer
        [HttpGet("/Category/{id}", Name = "category")]
        [HttpGet("/Category/{id}/page/{page}")]
        [EnableRateLimiting("SlidingWindow")]
        public async Task<IActionResult> Category(int id, [FromQuery] string keyword, [FromQuery] double min_price, [FromQuery] double max_price, [FromQuery] string orderby, int page = 1)
        {
            var products = await _productManager.GetProductsByCategoryAsync(id);
            var list = products.ToList();

            if (min_price != 0 || max_price != 0)
            {
                list = (await _productManager.FilterByPriceAsync(min_price, max_price, id)).ToList();
            }

            if (!string.IsNullOrEmpty(keyword))
            {
                list = (await _productManager.GetProductsAsync(keyword)).ToList();
            }

            if (!string.IsNullOrEmpty(orderby) || orderby != "Default")
            {
                switch (orderby)
                {
                    case "AscendingPrice":
                        list = (await _productManager.Filter(SortingType.AscendingPrice, id)).ToList();
                        break;
                    case "DescendingPrice":
                        list = (await _productManager.Filter(SortingType.DescendingPrice, id)).ToList();
                        break;
                    case "Popularity":
                        list = (await _productManager.Filter(SortingType.Popularity, id)).ToList();
                        break;
                    case "Date":
                        list = (await _productManager.Filter(SortingType.Date, id)).ToList();
                        break;
                    case "Rating":
                        list = (await _productManager.Filter(SortingType.Rating, id)).ToList();
                        break;
                }
            }

            int pageSize = 6;
            int totalProduct = list.Count;
            var totalPage = (int)Math.Ceiling((double)totalProduct / pageSize);

            var productsPerPage = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var model = new ProductModel
            {
                ProductList = productsPerPage,
                CategoryId = id,
                currentPage = page,
                totalPage = totalPage,
                totalProduct = totalProduct
            };
            foreach (var product in productsPerPage)
            {
                model.ProductListReview.Add(await _productManager.GetAvgRating(product.Id));
            }

            return View(model);
        }


        [EnableRateLimiting("SlidingWindow")]
        [HttpGet("/Product/{id}")]
        public async Task<IActionResult> Detail(int id)
        {
            var product = await _productManager.GetProductDetail(id);
            if (product == null)
            {
                return NotFound("Sản phẩm không tồn tại hoặc đã bị xóa");
            }
            var reviews = (await _reviewerFinder.GetReview(id)).ToList();
            List<string> reviewerName = [];
            foreach (var review in reviews)
            {
                reviewerName.Add((await _reviewerFinder.GetUserName(review.UserId)));
            }
            var model = new ProductModel
            {
                Id = id,
                Name = product.Name,
                Brand = product.Brand,
                Quantity = product.Quantity,
                Price = product.Price,
                CategoryId = product.CategoryId,
                Description = product.Description,
                Sold = product.Sold,
                Image = product.ImageUrl ?? string.Empty,
                Rating = await _productManager.GetAvgRating(id),
                Reviews = reviews,
                ReviewerName = reviewerName,
                oldPrice = product.OldPrice,
                CategoryName = (await _productManager.GetAllCategoriesAsync()).FirstOrDefault(c => c.Id == product.CategoryId)?.Name ?? string.Empty
            };
            return View(model);
        }


        #endregion

        #region Manager

        [Authorize(Roles = "Admin")]
        [HttpGet("/Manage")]
        public async Task<IActionResult> Manage()
        {
            var cate = await _productManager.GetAllCategoriesAsync();
            var model = new CategoryModel
            {
                CategoryList = cate.SkipLast(1).ToList()
            };
            return View(model);
        }


        [Authorize(Roles = "Admin")]
        [HttpGet("/Product-List")]
        public async Task<IActionResult> ProductList()
        {
            var model = new ProductModel
            {
                ProductList = (await _productManager.GetProductsAsync()).ToList()
            };
            return View(model);
        }


        [Authorize(Roles = "Admin")]
        [HttpPost("/Add")]
        [RequestFormLimits(ValueLengthLimit = 3 * 1024 * 1024)]
        public async Task<IActionResult> Add(ProductModel model)
        {
            try { model.Description = _editorContent.PrepareForSave(model.Description, product: true); }
            catch (EditorImageException exception)
            {
                ModelState.AddModelError(nameof(model.Description), exception.Message);
                ViewData["ProductForm"] = model;
                return View("Manage", new CategoryModel { CategoryList = (await _productManager.GetAllCategoriesAsync()).SkipLast(1).ToList() });
            }
            if (!ModelState.IsValid)
            {
                ViewData["ProductForm"] = model;
                return View("Manage", new CategoryModel { CategoryList = (await _productManager.GetAllCategoriesAsync()).SkipLast(1).ToList() });
            }
            try
            {
                var product = new Product
                {
                    Name = model.Name,
                    Price = model.Price,
                    OldPrice = 0,
                    CategoryId = model.CategoryId,
                    Brand = model.Brand,
                    Date_Import = model.Date_Import,
                    Description = model.Description,
                    Quantity = model.Quantity,
                    ImageUrl = string.Empty,
                };

                if (model.ImageUrl != null && model.ImageUrl.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products");
                    Directory.CreateDirectory(uploadsFolder);

                    var ext = Path.GetExtension(model.ImageUrl.FileName); 

                    // Sinh GUID không dấu gạch ngang và thêm timestamp
                    var guidPart = Guid.NewGuid().ToString("N");            
                    var timePart = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    var safeName = $"{guidPart}_{timePart}{ext}"; 

                    var fullPath = Path.Combine(uploadsFolder, safeName);
                    using var stream = new FileStream(fullPath, FileMode.Create);
                    await model.ImageUrl.CopyToAsync(stream);

                    // Gán lại tên file (đã sanitize) cho product
                    product.ImageUrl = safeName;
                }
                var saved = await _productManager.AddProductAsync(product);

                if (!string.IsNullOrEmpty(model.Coupon))
                {
                    await _productManager.AddOrUpdateCouponAsync(saved.Id, model.Coupon, model.Discount);
                }
                TempData["Message"] = "Thêm sản phẩm thành công";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add product");
                TempData["Message"] = "Thêm sản phẩm thất bại";
            }
            return RedirectToAction("Manage");
        }


        [Authorize(Roles = "Admin")]
        [HttpGet("/Remove/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var uploadsPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products");
            try
            {
                await _productManager.DeleteProductAsync(id, uploadsPath);
                TempData["Message"] = "Xóa sản phẩm thành công!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                TempData["Message"] = "Xóa sản phẩm thất bại!";
            }
            return RedirectToAction("ProductList");
        }


        [Authorize(Roles = "Admin")]
        [HttpGet("/Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _productManager.GetProductDetail(id);
            if (product == null) return NotFound();
            var model = new ProductModel
            {
                Id = id,
                Name = product.Name,
                Price = product.Price,
                CategoryId = product.CategoryId,
                Sold = product.Sold,
                oldPrice = product.OldPrice,
                Brand = product.Brand,
                Date_Import = product.Date_Import,
                Description = EditorContentService.ProductEditorHtml(product.Description),
                Quantity = product.Quantity,
                CategoryList = (await _productManager.GetAllCategoriesAsync()).SkipLast(1).ToList(),
                Image = product.ImageUrl ?? string.Empty
            };
            var discount = await _productManager.GetCouponAsync(id);
            if (discount != null)
            {
                model.Coupon = discount.Coupon;
                model.Discount = discount.DiscountPercent;
            }
            return View(model);
        }


        [Authorize(Roles = "Admin")]
        [HttpPost("/Edit/{id}")]
        [RequestFormLimits(ValueLengthLimit = 3 * 1024 * 1024)]
        public async Task<IActionResult> Edit(ProductModel model, [FromRoute] int id)
        {
            model.Id = id;
            string description;
            try { description = _editorContent.PrepareForSave(model.Description, product: true); }
            catch (EditorImageException exception)
            {
                ModelState.AddModelError(nameof(model.Description), exception.Message);
                return View("Edit", model);
            }
            var existing = await _productManager.GetProductDetail(id);
            if (existing == null) return NotFound();
            if (ModelState.IsValid)
            {
                var product = new Product
                {
                    Id = id,
                    Name = model.Name,
                    Price = model.Price,
                    CategoryId = model.CategoryId,
                    Brand = model.Brand,
                    Sold = model.Sold,
                    Date_Import = model.Date_Import,
                    Description = description,
                    Quantity = model.Quantity,
                };

                // Check if the price is lower than the previous price
                if (product.Price < existing.Price)
                {
                    product.OldPrice = existing.Price;
                    model.oldPrice = existing.Price;
                }
                else
                {
                    product.OldPrice = existing.OldPrice;
                    model.oldPrice = existing.OldPrice;
                }

                if (model.ImageUrl != null && model.ImageUrl.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products");
                    Directory.CreateDirectory(uploadsFolder);

                    var ext = Path.GetExtension(model.ImageUrl.FileName);

                    // Sinh GUID không dấu gạch ngang và thêm timestamp
                    var guidPart = Guid.NewGuid().ToString("N");
                    var timePart = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    var safeName = $"{guidPart}_{timePart}{ext}";

                    var fullPath = Path.Combine(uploadsFolder, safeName);
                    using var stream = new FileStream(fullPath, FileMode.Create);
                    await model.ImageUrl.CopyToAsync(stream);

                    // Gán lại tên file (đã sanitize) cho product
                    product.ImageUrl = safeName;
                }
                model.Image = product.ImageUrl ?? existing.ImageUrl ?? string.Empty;

                await _productManager.UpdateProductAsync(product);
                if (!string.IsNullOrEmpty(model.Coupon))
                {
                    await _productManager.AddOrUpdateCouponAsync(product.Id, model.Coupon, model.Discount);
                }
                TempData["Notify"] = "Cập nhật sản phẩm thành công";
            }
            else
            {
                TempData["Notify"] = "Cập nhật sản phẩm thất bại";
            }
            return View("Edit", model);
        }


        #endregion


        [HttpGet("/api/get/products")]
        public async Task<IActionResult> ProductsApi()
        {
            try
            {
                var products = (await _productManager.GetProductsAsync()).ToList();
                return Ok(products.Select(p => new
                {
                    p.Id,
                    p.Quantity
                }).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching products from API");
                return StatusCode(500, "Internal server error while fetching products.");
            }
        }
    }
}
