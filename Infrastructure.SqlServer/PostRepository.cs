
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.SqlServer
{
    public class PostRepository : IPostRepository
    {
        private readonly ShopContext _context;
        private readonly IMapper _mapper;

        public PostRepository(ShopContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public Task<Entities.Post> AddPostAsync(Entities.Post post)
        {
            throw new NotImplementedException();
        }

        public Task<Entities.Post> DeletePostAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<Entities.Post>> FindPostsAsync(string keyword)
        {
            var posts = await _context.Posts.Where(p => EF.Functions.Collate(p.Title, "SQL_Latin1_General_CP1_CI_AI").Contains(keyword)).ToListAsync();
            return _mapper.Map<IEnumerable<Entities.Post>>(posts);
        }

        public Task<IEnumerable<Entities.Post>> GetPostsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Entities.Post>> GetPostsAsync(string categoryId)
        {
            throw new NotImplementedException();
        }

        public Task<Entities.Post> UpdatePostAsync(Entities.Post post)
        {
            throw new NotImplementedException();
        }
    }
}
