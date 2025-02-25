using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.Business_Logic
{
    public interface IUserManage
    {
        Task<User> LoginAsync(string username, string password);
        Task<ValidationResult> Validate(string username, string password, string confirmPassword, string email, bool passwordValid);
        Task<User> SignUpAsync(User user);
        Task<User> GetUserAsync(string UoE);
        Task<User> GetUserAsync(int id);
        Task<string> GetRoleNameAsync(int id);
        Task<Review> AddReviewAsync(int userId, int productId, int rating, string? comment = null);
        Task<Post> AddPostAsync(int userId, string title, string content, string image);
        Task<Post> UpdatePostAsync(Post post);
        Task<Post> DeletePostAsync(Guid id);
        Task<IEnumerable<Post>> GetPostsAsync(string keyword);
        Task<IEnumerable<Post>> GetPostsAsync();
        Task<Post> GetPostDetailAsync(Guid id); 
    }
}
