using Entities;
using UseCase.UnitOfWork;

namespace UseCase
{
    public class ReviewerFinder : IReviewerFinder
    {
        private readonly IReviewUnitOfWork _unitOfWork;

        public ReviewerFinder(IReviewUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<Review> AddReviewAsync(int userId, int productId, int rating, string? comment = null)
        {
            return await _unitOfWork.ReviewRepository.AddReviewAsync(new Review
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProductId = productId,
                Rating = rating,
                Comment = comment,
                CreatedAt = DateTime.Now
            });
        }

        public async Task<IEnumerable<Review>> GetReview(int productId)
        {
            return await _unitOfWork.ReviewRepository.GetReviewAsync(productId);
        }

        public async Task<string> GetUserName(int id)
        {
            var user = await _unitOfWork.UserRepository.GetUserAsync(id) ?? throw new();
            return user.DisplayName;
        }
    }
}
