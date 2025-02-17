

namespace Infrastructure.SqlServer
{
    public class ItemInforRepository : IItemInforRepository
    {
        private readonly ShopContext _context;
        private readonly IMapper _mapper;

        public ItemInforRepository(ShopContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        public Task<Entities.ItemInfor> GetItemInforAsync(int productId)
        {
            throw new NotImplementedException();
        }
    }
}