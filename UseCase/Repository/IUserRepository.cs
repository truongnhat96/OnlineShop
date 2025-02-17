using Entities;

namespace UseCase.Repository
{
    public interface IUserRepository
    {
        Task<User> AddAccountAsync(User user);
        Task<User?> GetUserAsync(string usernameORemail);
        Task<User?> GetUserAsync(int id);
        bool EmailExists(string email);
    }
}
