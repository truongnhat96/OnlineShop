using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.Repository
{
    public interface IDiscountUsageRepository
    {
        public Task AddUsageAsync(DiscountUsage Usage);
        public Task<DiscountUsage?> GetUsageAsync(int userId, int discountId);
    }
}
