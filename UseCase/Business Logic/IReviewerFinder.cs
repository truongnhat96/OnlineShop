using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.UnitOfWork
{
    public interface IReviewerFinder
    {
        Task<Review> AddReviewAsync(int userId, int productId, int rating, string? comment = null);
        Task<string> GetUserName(int id);
        Task<IEnumerable<Review>> GetReview(int productId);
    }
}
