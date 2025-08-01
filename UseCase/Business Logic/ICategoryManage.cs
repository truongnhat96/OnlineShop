using Entities;

namespace UseCase.Business_Logic
{
    public interface ICategoryManage
    {
        Task<IEnumerable<Category>> GetAllCategoriesAsync();
    }
}