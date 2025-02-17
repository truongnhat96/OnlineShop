using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.Repository
{
    public interface ICategoryRepository
    {
        Task<Category> GetCategoryPostAsync();
        Task<IEnumerable<Category>> GetAllCategoriesAsync();
    }
}
