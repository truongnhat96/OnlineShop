using Microsoft.EntityFrameworkCore;

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

        public async Task AddAsync(Entities.ItemInfor itemInfor)
        {
            var entity = _mapper.Map<Entities.ItemInfor, ItemInfor>(itemInfor);
            await _context.ItemInfors.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public Task DeleteAsync(int productId)
        {
            throw new NotImplementedException();
        }

        public async Task<Entities.ItemInfor?> GetItemInforAsync(int id)
        {
            var entity = await _context.ItemInfors.FindAsync(id);
            return _mapper.Map<ItemInfor?, Entities.ItemInfor?>(entity);
        }

        public async Task<IEnumerable<Entities.ItemInfor>> GetItemsInforAsync(int productId)
        {
            var entities = await _context.ItemInfors.AsNoTracking().Where(item => item.ProductId == productId).ToListAsync() ?? [];
            return _mapper.Map<IEnumerable<ItemInfor>, IEnumerable<Entities.ItemInfor>>(entities);
        }

        public Task UpdateAsync(Entities.ItemInfor itemInfor)
        {
            var entity = _mapper.Map<Entities.ItemInfor, ItemInfor>(itemInfor);
            _context.ItemInfors.Update(entity);
            return _context.SaveChangesAsync();
        }
    }
}