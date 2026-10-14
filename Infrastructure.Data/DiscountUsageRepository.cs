using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer
{
    public class DiscountUsageRepository : IDiscountUsageRepository
    {
        private readonly ShopContext _context;
        private readonly IMapper _mapper;

        public DiscountUsageRepository(ShopContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        public async Task AddUsageAsync(Entities.DiscountUsage Usage)
        {
            var discountUsage = _mapper.Map<Entities.DiscountUsage, DiscountUsage>(Usage);
            await _context.DiscountUsages.AddAsync(discountUsage);
            await _context.SaveChangesAsync();
        }

        public async Task<Entities.DiscountUsage?> GetUsageAsync(int userId, int discountId)
        {
            var discountUsage = await _context.DiscountUsages.FirstOrDefaultAsync(x => x.UserId == userId && x.DiscountId == discountId);
            return _mapper.Map<DiscountUsage?, Entities.DiscountUsage>(discountUsage);
        }
    }
}
