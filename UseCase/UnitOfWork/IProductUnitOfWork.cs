using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UseCase.Repository;

namespace UseCase.UnitOfWork
{
    public interface IProductUnitOfWork
    {
        IProductRepository ProductRepository { get; }
        IItemInforRepository ItemInforRepository { get; }
        ICategoryRepository CategoryRepository { get; }
        IReviewRepository ReviewRepository { get; }
        Task SaveAsync();
        IUserRepository UserRepository { get; }
    }
}
