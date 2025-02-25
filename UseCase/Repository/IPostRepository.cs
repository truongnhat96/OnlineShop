using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.Repository
{
    public interface IPostRepository
    {
        Task<IEnumerable<Post>> GetPostsAsync();
        Task<IEnumerable<Post>> FindPostsAsync(string keyword);
        Task<Post> AddPostAsync(Post post);
        Task<Post> UpdatePostAsync(Post post);
        Task<Post> DeletePostAsync(Guid id);
        Task<Post> GetPostAsync(Guid id);
    }
}
