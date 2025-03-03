using Microsoft.EntityFrameworkCore;

namespace Infrastructure.SqlServer
{
    public class DiscountRepository : IDiscountRepository
    {
        private readonly ShopContext _context;
        private readonly IMapper _mapper;

        public DiscountRepository(ShopContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        public async Task AddDiscountAsync(Entities.Discount discount)
        {
            var entity = _mapper.Map<Entities.Discount, Discount>(discount);
            await _context.Discounts.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<Entities.Discount?> GetDiscountAsync(int Id)
        {
            var entity = await _context.Discounts.FindAsync(Id);
            return _mapper.Map<Discount?, Entities.Discount>(entity);
        }

        public async Task UpdateDiscountAsync(Entities.Discount discount)
        {
            var entityEntry = _context.Entry(_context.Discounts.Find(discount.Id) ?? throw new("entity not exsist"));
            entityEntry.State = EntityState.Detached;
            var entity = _mapper.Map<Entities.Discount, Discount>(discount);
            _context.Discounts.Update(entity);
            await _context.SaveChangesAsync();
        }
    }
}
