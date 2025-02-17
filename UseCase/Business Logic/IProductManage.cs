using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.Business_Logic
{
    public interface IProductManage
    {
        Task<Product> AddProductAsync(Product product);
        Task<ItemInfor> AddInforAsync(int productId, ItemInfor item);
        Task<Product> UpdateProductAsync(Product product);
        Task<Product> DeleteProductAsync(int id);
        Task<IEnumerable<Product>> GetProductsByCategoryAsync(int categoryId);
        Task<IEnumerable<Product>> GetProductsAsync();
        Task<IEnumerable<Product>> GetProductsAsync(string keyword);
        Task<IEnumerable<Product>> FilterByPriceAsync(double firstPrice, double lastPrice, int id);
        Task<IEnumerable<Product>> Filter(SortingType type, int id);
        Task<Product> GetProductDetail(int id);
        Task<ItemInfor> GetItem(int productId);
        Task<IEnumerable<Review>> GetReview(int productId);
        Task<Category> GetCategoryPostAsync();
        Task<string> GetUserName(int id);
        Task<IEnumerable<Category>> GetAllCategoriesAsync();
        Task<int> GetAvgRating(int productId);
    }
}
