using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.Business_Logic
{
    public interface ICartManage
    {
        Task AddCartItemAsync(int productId, int userId, int quantity);
        Task UpdateCartItemAsync(int productId, int userId, int quantity);
        Task RemoveCartItemAsync(int productId, int userId);
        Task OrderProcessingAsync(int userId, int discountId);
        Task<bool> IsCouponUsed(int userId, int discountId);
        Task<Product> GetProductInCartAsync(int productId);
        Task<Product> GetProductInCartAfterDiscountAsync(int productId, string coupon);
        Task<IEnumerable<CartItem>> GetCartItemsAsync(int userId);
    }
}
