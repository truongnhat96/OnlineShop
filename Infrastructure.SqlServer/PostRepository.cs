﻿using Microsoft.EntityFrameworkCore;

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

        public async Task<Entities.Post> AddPostAsync(Entities.Post post)
        {
            var postDb = _mapper.Map<DataContext.Post>(post);
            await _context.Posts.AddAsync(postDb);
            await _context.SaveChangesAsync();
            return post;
        }

        public async Task<Entities.Post> DeletePostAsync(Guid id)
        {
            var postDb = await _context.Posts.FindAsync(id) ?? throw new("This Post is Not Exist");
            _context.Posts.Remove(postDb);
            await _context.SaveChangesAsync();
            return _mapper.Map<Entities.Post>(postDb);
        }

        public async Task<IEnumerable<Entities.Post>> FindPostsAsync(string keyword)
        {
            var posts = await _context.Posts.Where(p => EF.Functions.Collate(p.Title, "SQL_Latin1_General_CP1_CI_AI").Contains(keyword)).ToListAsync();
            return _mapper.Map<IEnumerable<Entities.Post>>(posts);
        }

        public async Task<Entities.Post?> GetPostAsync(Guid id)
        {
            var postDb = await _context.Posts.FindAsync(id);
            return _mapper.Map<Entities.Post>(postDb);
        }

        public async Task<IEnumerable<Entities.Post>> GetPostsAsync()
        {
            var postDb = await _context.Posts.ToListAsync();
            return _mapper.Map<IEnumerable<Entities.Post>>(postDb);
        }


        public async Task<Entities.Post> UpdatePostAsync(Entities.Post post)
        {
            var postDb = _mapper.Map<Entities.Post, Post>(post);
            _context.Posts.Update(postDb);
            await _context.SaveChangesAsync();
            return post;
        }
    }
}
