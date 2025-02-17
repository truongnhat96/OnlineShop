using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public Task<ItemInfor> AddInforAsync(int productId, ItemInfor item)
        {
            throw new NotImplementedException();
        }

        public async Task<Product> AddProductAsync(Product product)
        {
            return await _productUnitOfWork.ProductRepository.AddProductAsync(product);
        }

        public async Task<Product> DeleteProductAsync(int id)
        {
            return await _productUnitOfWork.ProductRepository.DeleteProductAsync(id);
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

        public Task<ItemInfor> GetItem(int productId)
        {
            throw new NotImplementedException();
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

        public async Task<IEnumerable<Review>> GetReview(int productId)
        {
            return await _productUnitOfWork.ReviewRepository.GetReviewAsync(productId);
        }

        public async Task<string> GetUserName(int id)
        {
            var user = await _productUnitOfWork.UserRepository.GetUserAsync(id) ?? throw new();
            return user.DisplayName;
        }

        public async Task<Product> UpdateProductAsync(Product product)
        {
            return await _productUnitOfWork.ProductRepository.UpdateProductAsync(product);
        }
    }
}
