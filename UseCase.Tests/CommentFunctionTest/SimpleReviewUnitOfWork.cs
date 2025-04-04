using UseCase.Repository;
using UseCase.UnitOfWork;   

namespace UseCase.Tests.CommentFunctionTest
{
    public class SimpleReviewUnitOfWork : IReviewUnitOfWork
    {
        public SimpleReviewUnitOfWork(IReviewRepository reviewRepository, IUserRepository userRepository) 
        {
            ReviewRepository = reviewRepository;
            UserRepository = userRepository;
        }
        public IReviewRepository ReviewRepository { get; }
        public IUserRepository UserRepository { get; }
    }
}
