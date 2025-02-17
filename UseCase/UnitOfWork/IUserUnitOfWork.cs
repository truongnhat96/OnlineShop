using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UseCase.Repository;

namespace UseCase.UnitOfWork
{
    public interface IUserUnitOfWork
    {
        IUserRepository UserRepository { get; }
        IRoleRepository RoleRepository { get; }
        IPostRepository PostRepository { get; }
        IReviewRepository ReviewRepository { get; }
        Task SaveAsync();
    }
}
