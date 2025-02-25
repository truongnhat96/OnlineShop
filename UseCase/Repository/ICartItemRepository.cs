using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.Repository
{
    public interface ICartItemRepository
    {
        Task AddCartAsync(CartItem cartItem);
        Task RemoveCartAsync(int productId);
        Task UpdateCartAsync(CartItem cartItem);
        Task<IEnumerable<CartItem>> GetCartItemsAsync(int userId);
        /// <summary>
        /// Get cart item by product id with no tracking
        /// </summary>
        /// <param name="productId"></param>
        /// <returns></returns>
        Task<CartItem?> GetCartItemAsync(int productId);
    }
}
