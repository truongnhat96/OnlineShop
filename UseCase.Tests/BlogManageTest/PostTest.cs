using Entities;
using Moq;
using UseCase.Repository;

namespace UseCase.Tests.BlogManageTest
{
    public class PostTest
    {
        [Fact]
        public async Task Add_Test()
        {
            var repo = new Mock<IPostRepository>();
            var blog = new Post 
            {
                UserId = 1,
                Title = "Test",
                Content = "Test content",
                ImageUrl = "test.jpg",
                CreatedAt = DateTime.Now
            };
            repo.Setup(p => p.AddPostAsync(blog)).ReturnsAsync(blog);

            var realRepo = repo.Object;
            var result = await realRepo.AddPostAsync(blog);

            Assert.NotNull(result);
            Assert.Equal(blog.Title, result.Title);
        }

        [Fact]
        public async Task Get_Test()
        {
            var repo = new Mock<IPostRepository>();
            var blog = new Post
            {
                Id = Guid.NewGuid(),
                UserId = 1,
                Title = "Test",
                Content = "Test content",
                ImageUrl = "test.jpg",
                CreatedAt = DateTime.Now
            };
            repo.Setup(p => p.GetPostAsync(blog.Id)).ReturnsAsync(blog);
            repo.Setup(p => p.GetPostsAsync()).ReturnsAsync([blog]);
            repo.Setup(p => p.FindPostsAsync("Te")).ReturnsAsync([blog]);

            var postManage = new PostManage(repo.Object);
            var result = await postManage.GetPostDetailAsync(blog.Id);
            var resultList = await postManage.GetPostsAsync();
            var resultListByKeyword = await postManage.GetPostsAsync("Te");

            Assert.NotNull(result);
            Assert.Equal(blog.Title, result.Title);
            Assert.NotNull(resultList);
            Assert.NotEmpty(resultList);
            Assert.Equal(blog.Title, resultListByKeyword.First().Title);
        }

        [Fact]
        public async Task Delete_Test()
        {
            var repo = new Mock<IPostRepository>();
            var blog = new Post
            {
                Id = Guid.NewGuid(),
                UserId = 1,
                Title = "Test",
                Content = "Test content",
                ImageUrl = "test.jpg",
                CreatedAt = DateTime.Now
            };
            repo.Setup(p => p.DeletePostAsync(blog.Id)).ReturnsAsync(blog);
            repo.Setup(p => p.GetPostAsync(blog.Id));

            var postManage = new PostManage(repo.Object);
            var result = await postManage.DeletePostAsync(blog.Id);
            var resultGet = await repo.Object.GetPostAsync(blog.Id);

            Assert.NotNull(result);
            Assert.Null(resultGet);
        }

        [Fact]
        public async Task Update_Test()
        {
            var repo = new Mock<IPostRepository>();
            var blog = new Post
            {
                Id = Guid.NewGuid(),
                UserId = 1,
                Title = "Test",
                Content = "Test content",
                ImageUrl = "test.jpg",
                CreatedAt = DateTime.Now
            };
            repo.Setup(p => p.UpdatePostAsync(blog)).ReturnsAsync(blog);

            var postManage = new PostManage(repo.Object);
            var result = await postManage.UpdatePostAsync(blog);

            Assert.NotNull(result);
            Assert.Equal(blog.Title, result.Title);
        }
    }
}
