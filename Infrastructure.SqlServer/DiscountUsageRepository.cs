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
        public Task AddUsageAsync(Entities.DiscountUsage Usage)
        {
            throw new NotImplementedException();
        }

        public Task<Entities.DiscountUsage?> GetUsageAsync(int userId, int discountId)
        {
            throw new NotImplementedException();
        }
    }
}
