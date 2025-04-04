using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UseCase.Repository;

namespace UseCase.UnitOfWork
{
    public interface IReviewUnitOfWork
    {
        public IReviewRepository ReviewRepository { get; }
        public IUserRepository UserRepository { get; }
    }
}
