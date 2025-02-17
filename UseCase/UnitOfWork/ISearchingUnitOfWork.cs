using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UseCase.Repository;

namespace UseCase.UnitOfWork
{
    public interface ISearchingUnitOfWork
    {
        IProductRepository ProductRepository { get; }
        IPostRepository PostRepository { get; }
        Task SaveAsync();
    }
}
