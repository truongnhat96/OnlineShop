using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UseCase.Repository;

namespace UseCase.UnitOfWork
{
    public interface ICartItemUnitOfWork
    {
        IUserRepository UserRepository { get; }
        IProductRepository ProductRepository { get; }
        ICartItemRepository CartItemRepository { get; }
        IDiscountUsageRepository DiscountUsageRepository { get; }
        IDiscountRepository DiscountRepository { get; }
        Task SaveAsync();
    }
}
