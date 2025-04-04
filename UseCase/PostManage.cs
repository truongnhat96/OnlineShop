using Entities;
using UseCase.Business_Logic;
using UseCase.Repository;

namespace UseCase
{
    public class PostManage : IPostManage
    {
        private readonly IPostRepository _postRepository;

        public PostManage(IPostRepository postRepository)
        {
            _postRepository = postRepository;
        }
        public async Task<Post> AddPostAsync(int userId, string title, string content, string image)
        {
            return await _postRepository.AddPostAsync(new Post
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Content = content,
                ImageUrl = image,
                CreatedAt = DateTime.Now
            }); 
        }

        public async Task<Post> DeletePostAsync(Guid id)
        {
            return await _postRepository.DeletePostAsync(id);
        }

        public async Task<Post> GetPostDetailAsync(Guid id)
        {
            return await _postRepository.GetPostAsync(id) ?? throw new ArgumentNullException("Post not found");
        }

        public async Task<IEnumerable<Post>> GetPostsAsync(string keyword)
        {
            return await _postRepository.FindPostsAsync(keyword);
        }

        public async Task<IEnumerable<Post>> GetPostsAsync()
        {
            return await _postRepository.GetPostsAsync();
        }

        public async Task<Post> UpdatePostAsync(Post post)
        {
            return await _postRepository.UpdatePostAsync(post);
        }
    }
}
