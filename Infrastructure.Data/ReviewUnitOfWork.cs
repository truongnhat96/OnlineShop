using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer
{
    public class ReviewUnitOfWork : IReviewUnitOfWork
    {
        private readonly ShopContext _context;
        private readonly IMapper _mapper;
        public ReviewUnitOfWork(ShopContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
            ReviewRepository = new ReviewRepository(_context, _mapper);
            UserRepository = new UserRepository(_context, _mapper);
        }

        public IReviewRepository ReviewRepository { get; }
        public IUserRepository UserRepository { get; }
    }
}
