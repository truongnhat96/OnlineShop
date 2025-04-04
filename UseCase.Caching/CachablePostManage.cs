using Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using UseCase.Business_Logic;
using UseCase.CachingSupport;
using UseCase.Repository;

namespace UseCase.Caching
{
    public class CachablePostManage : IPostManage
    {
        private readonly IPostRepository _postRepository;
        private readonly DistributedCacheEntryOptions _cacheOptions;
        private readonly IDistributedCache _cache;
        private readonly CachablePostSupportOption _option;
        private readonly ILogger<CachablePostManage> _logger;


        public CachablePostManage(IPostRepository postRepository, IDistributedCache cache, CachablePostSupportOption option, ILogger<CachablePostManage> logger)
        {
            _postRepository = postRepository;
            _cache = cache;
            _option = option;
            _logger = logger;
            _cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _option.CacheLifeTime
            };
        }
        public async Task<Post> AddPostAsync(int userId, string title, string content, string image)
        {
            var post = await _postRepository.AddPostAsync(new Post
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Content = content,
                ImageUrl = image,
            });
            var cacheKey = _option.CacheKey;
            var Posts = await _postRepository.GetPostsAsync();
            _logger.LogInformation("Storing data to cache...");
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(Posts), _cacheOptions);
            return post;
        }


        public async Task<Post> DeletePostAsync(Guid id)
        {
            var post = await _postRepository.DeletePostAsync(id);
            var cacheKey = _option.CacheKey;
            var Posts = await _postRepository.GetPostsAsync();
            _logger.LogInformation("Storing data to cache...");
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(Posts), _cacheOptions);
            return post;
        }

        public async Task<Post> UpdatePostAsync(Post post)
        {
            return await _postRepository.UpdatePostAsync(post);
        }

        public async Task<Post> GetPostDetailAsync(Guid id)
        {
            var cacheKey = _option.CacheKey;
            var data = await _cache.GetStringAsync(cacheKey);
            if (data == null)
            {
                return await _postRepository.GetPostAsync(id) ?? throw new("Post not found");
            }
            else
            {
                var posts = JsonSerializer.Deserialize<IEnumerable<Post>>(data) ?? throw new("No Data In Cache");
                var post = posts.FirstOrDefault(p => p.Id == id);
                if (post == null)
                {
                    return await _postRepository.GetPostAsync(id) ?? throw new("Post not found");
                }
                return post;
            }
        }

        public async Task<IEnumerable<Post>> GetPostsAsync(string keyword)
        {
            return await _postRepository.FindPostsAsync(keyword);
        }

        public async Task<IEnumerable<Post>> GetPostsAsync()
        {
            var cacheKey = _option.CacheKey;
            var data = await _cache.GetStringAsync(cacheKey);
            if (data == null)
            {
                var Posts = await _postRepository.GetPostsAsync();
                _logger.LogInformation("Storing data to cache...");
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(Posts), _cacheOptions);
                return Posts;
            }
            else
            {
                _logger.LogInformation("Getting data from cache...");
                return JsonSerializer.Deserialize<IEnumerable<Post>>(data)!;
            }
        }
    }
}
