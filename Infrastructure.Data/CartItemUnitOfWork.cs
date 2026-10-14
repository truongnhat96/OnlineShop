using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer
{
    public class CartItemUnitOfWork : ICartItemUnitOfWork
    {
        private readonly ShopContext _context;
        private readonly IMapper _mapper;

        public CartItemUnitOfWork(ShopContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;

            UserRepository = new UserRepository(context, mapper);
            ProductRepository = new ProductRepository(context, mapper);
            CartItemRepository = new CartItemRepository(context, mapper);
            DiscountUsageRepository = new DiscountUsageRepository(context, mapper);
            DiscountRepository = new DiscountRepository(context, mapper);
        }
        public IUserRepository UserRepository { get; }
        public IProductRepository ProductRepository { get; }
        public ICartItemRepository CartItemRepository { get; }
        public IDiscountUsageRepository DiscountUsageRepository { get; }
        public IDiscountRepository DiscountRepository { get; }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
