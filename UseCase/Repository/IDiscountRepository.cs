using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.Repository
{
    public interface IDiscountRepository
    {
        /// <summary>
        /// Get coupon discount by ProductId
        /// </summary>
        /// <param name="Id">Id is ProductId</param>
        /// <returns></returns>
        Task<Discount?> GetDiscountAsync(int Id);
        Task AddDiscountAsync(Discount discount);
        Task UpdateDiscountAsync(Discount discount);
    }
}
