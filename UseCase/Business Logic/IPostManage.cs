using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.Business_Logic
{
    public interface IPostManage
    {
        Task<Post> AddPostAsync(int userId, string title, string content, string image);
        Task<Post> UpdatePostAsync(Post post);
        Task<Post> DeletePostAsync(Guid id);
        Task<IEnumerable<Post>> GetPostsAsync(string keyword);
        Task<IEnumerable<Post>> GetPostsAsync();
        Task<Post> GetPostDetailAsync(Guid id);
    }
}
