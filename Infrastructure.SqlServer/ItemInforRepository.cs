


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

        public Task AddAsync(Entities.ItemInfor itemInfor)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(int productId)
        {
            throw new NotImplementedException();
        }

        public Task<Entities.ItemInfor> GetItemInforAsync(int productId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(Entities.ItemInfor itemInfor)
        {
            throw new NotImplementedException();
        }
    }
}