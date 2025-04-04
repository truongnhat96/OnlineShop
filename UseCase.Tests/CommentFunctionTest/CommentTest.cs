using Entities;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UseCase.Repository;

namespace UseCase.Tests.CommentFunctionTest
{
    public class CommentTest
    {
        [Fact]
        public async Task Test()
        {
            var reviewRepo = new Mock<IReviewRepository>();
            var userRepo = new Mock<IUserRepository>();

            var commentTest = new Review
            {
                UserId = 6,
                ProductId = 34,
                Rating = 5,
                Comment = "Great product!",
                CreatedAt = DateTime.Now
            };

            reviewRepo.Setup(r => r.AddReviewAsync(commentTest))
                .ReturnsAsync(commentTest);

            userRepo.Setup(u => u.GetUserAsync(commentTest.UserId))
                .ReturnsAsync(new User
                {
                    Id = commentTest.UserId,
                    Email = "jtest@gmail.com",
                    DisplayName = "John Doe",
                    Password = "12999",
                    RoleId = 2,
                    Username = "jtest"
                });

            var reviewUoW = new SimpleReviewUnitOfWork(reviewRepo.Object, userRepo.Object);
            var reviewerFinder = new ReviewerFinder(reviewUoW);

            var result = await reviewUoW.ReviewRepository.AddReviewAsync(commentTest);
            var result1 = await reviewerFinder.GetUserName(commentTest.UserId);
            Assert.Equal(commentTest.UserId, result.UserId);
            Assert.True(result1 == "John Doe");
        }
    }
}
