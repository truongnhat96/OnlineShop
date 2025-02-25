using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.Repository
{
    public interface IItemInforRepository
    {
        /// <summary>
        /// Get item information of product
        /// </summary>
        /// <param name="id">id is Product ID</param>
        /// <returns></returns>
        Task<ItemInfor> GetItemInforAsync(int productId);
        Task AddAsync(ItemInfor itemInfor);
        Task UpdateAsync(ItemInfor itemInfor);
        Task DeleteAsync(int productId);
    }
}
