using Entities;

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

    }
}
