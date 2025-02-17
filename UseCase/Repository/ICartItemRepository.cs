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
        Task AddCartItemAsync(CartItem cartItem);
        Task RemoveCartItemAsync(CartItem cartItem);
        Task<IEnumerable<CartItem>> GetCartItemsAsync();
        Task<IEnumerable<CartItem>> GetCartItemsAsync(int productId);
    }
}
