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
        Task<CartItem> AddCartItemAsync(int productId, int quantity, bool isLogin);
        Task<CartItem> AddCartItemAsync(int productId, bool isLogin);
        Task<CartItem> RemoveCartItemAsync(int productId, bool isLogin);
        Task<IEnumerable<CartItem>> GetCartItemsAsync(bool isLogin);
        Task<double> GetTotalPriceAsync(int productId, bool isLogin);
    }
}
