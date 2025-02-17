using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer
{
    public class ProductUnitOfWork : IProductUnitOfWork
    {
        private readonly ShopContext _context;
        private readonly IMapper _mapper;

        public ProductUnitOfWork(ShopContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
            ProductRepository = new ProductRepository(context, mapper);
            ItemInforRepository = new ItemInforRepository(context, mapper);
            CategoryRepository = new CategoryRepository(context, mapper);
            ReviewRepository = new ReviewRepository(context, mapper);
            UserRepository = new UserRepository(context, mapper);
        }
        public IProductRepository ProductRepository { get; }

        public IItemInforRepository ItemInforRepository { get; }

        public ICategoryRepository CategoryRepository { get; }

        public IReviewRepository ReviewRepository { get; }

        public IUserRepository UserRepository { get; }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
