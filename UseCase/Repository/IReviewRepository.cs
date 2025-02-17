using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.Repository
{
    public interface IReviewRepository
    {
        Task<IEnumerable<Review>> GetReviewAsync(int productid);
        Task<Review> AddReviewAsync(Review review);
    }
}
