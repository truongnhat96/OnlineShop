using Entities;
using UseCase.Business_Logic;
using UseCase.UnitOfWork;

namespace UseCase
{
    public class ProductManage : IProductManage
    {
        private readonly IProductUnitOfWork _productUnitOfWork;

        public ProductManage(IProductUnitOfWork productUnitOfWork)
        {
            _productUnitOfWork = productUnitOfWork;
        }

        public async Task AddOrUpdateCouponAsync(int Id, string coupon, int discountPercent)
        {
            if (await _productUnitOfWork.DiscountRepository.GetDiscountAsync(Id) != null)
            {
                await _productUnitOfWork.DiscountRepository.UpdateDiscountAsync(new Discount
                {
                    Id = Id,
                    Coupon = coupon,
                    DiscountPercent = discountPercent
                });
            }
            else
            {
                await _productUnitOfWork.DiscountRepository.AddDiscountAsync(new Discount
                {
                    Id = Id,
                    Coupon = coupon,
                    DiscountPercent = discountPercent
                });
            }
        }

        public async Task<ItemInfor> AddInforAsync(ItemInfor item)
        {
            await _productUnitOfWork.ItemInforRepository.AddAsync(item);
            return item;
        }

        public async Task<Product> AddProductAsync(Product product)
        {
            return await _productUnitOfWork.ProductRepository.AddProductAsync(product);
        }

        public async Task<Product> DeleteProductAsync(int id, string? uploadsPath = default)
        {
            return await _productUnitOfWork.ProductRepository.DeleteProductAsync(id, uploadsPath);
        }

        public async Task<IEnumerable<Product>> Filter(SortingType type, int id)
        {
            return await _productUnitOfWork.ProductRepository.FilterBy(type, id);
        }

        public async Task<IEnumerable<Product>> FilterByPriceAsync(double firstPrice, double lastPrice, int id)
        {
            return await _productUnitOfWork.ProductRepository.FindProductsAsync(firstPrice, lastPrice, id);
        }

        public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
        {
            return await _productUnitOfWork.CategoryRepository.GetAllCategoriesAsync();
        }

        public async Task<int> GetAvgRating(int productId)
        {
            var reviews = await _productUnitOfWork.ReviewRepository.GetReviewAsync(productId);
            int cnt = 0;
            foreach (var review in reviews)
            {
                cnt += review.Rating;
            }
            try
            {
                return cnt / reviews.Count();
            }
            catch (DivideByZeroException)
            {
                return 0;
            } 
        }

        public async Task<Category> GetCategoryPostAsync()
        {
            return await _productUnitOfWork.CategoryRepository.GetCategoryPostAsync();
        }

        public async Task<Discount?> GetCouponAsync(int id)
        {
           return await _productUnitOfWork.DiscountRepository.GetDiscountAsync(id);
        }

        public async Task<IEnumerable<ItemInfor>> GetItems(int productId)
        {
           return await _productUnitOfWork.ItemInforRepository.GetItemsInforAsync(productId);
        }

        public async Task<Product> GetProductDetail(int id)
        {
            return await _productUnitOfWork.ProductRepository.GetProductAsync(id);
        }

        public async Task<IEnumerable<Product>> GetProductsAsync()
        {
            return await _productUnitOfWork.ProductRepository.GetProductsAsync();
        }

        public async Task<IEnumerable<Product>> GetProductsAsync(string keyword)
        {
            return await _productUnitOfWork.ProductRepository.FindProductsAsync(keyword);
        }

        public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(int categoryId)
        {
            return await _productUnitOfWork.ProductRepository.GetProductsAsync(categoryId);
        }


        public async Task<Product> UpdateProductAsync(Product product)
        {
            return await _productUnitOfWork.ProductRepository.UpdateProductAsync(product);
        }

        public async Task<ItemInfor?> GetItem(int id)
        {
            return await _productUnitOfWork.ItemInforRepository.GetItemInforAsync(id);
        }

        public async Task UpdateInforAsync(ItemInfor item)
        {
            await _productUnitOfWork.ItemInforRepository.UpdateAsync(item);
        }
    }
}
