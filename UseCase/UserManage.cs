using Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using UseCase.Business_Logic;
using UseCase.UnitOfWork;

namespace UseCase
{
    public class UserManage : IUserManage
    {
        private readonly IUserUnitOfWork _unitOfWork;

        public UserManage(IUserUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }


        public async Task<string> GetRoleNameAsync(int id)
        {
            var role = await _unitOfWork.RoleRepository.GetRoleAsync(id);
            return role.Name;
        }

        public async Task<User> GetUserAsync(string UoE)
        {
            return await _unitOfWork.UserRepository.GetUserAsync(UoE) ?? throw new ArgumentNullException("Email hoặc người dùng không tồn tại trong hệ thống");
        }

        public async Task<User> GetUserAsync(int id)
        {
            return await _unitOfWork.UserRepository.GetUserAsync(id) ?? throw new("User not exsist");
        }

        public async Task<User> LoginAsync(string username, string password)
        {
            var user = await _unitOfWork.UserRepository.GetUserAsync(username);
            if (user != null && user.Password == password)
            {
                return user;
            }
            throw new ArgumentNullException("Invalid username or password");
        }

        public async Task<User> SignUpAsync(User user)
        {
            return await _unitOfWork.UserRepository.AddAccountAsync(user);
        }

        public async Task<ValidationResult> Validate(string username, string password, string confirmPassword, string email, bool passwordValid)
        {
            if(password != confirmPassword)
            {
                return ValidationResult.PasswordNotMatch;
            }
            if (!Regex.IsMatch(username, @"^(?=.*[A-Za-z])(?=.*\d).+$") || username.Contains('@'))
            {
                return ValidationResult.UsernameNotValid;
            }
            var user = await _unitOfWork.UserRepository.GetUserAsync(username);
            if (user != null)
            {
                return ValidationResult.UserExists;
            }
            if(_unitOfWork.UserRepository.EmailExists(email))
            {
                return ValidationResult.EmailExists;
            }
            if (!passwordValid)
            {
                return ValidationResult.PasswordTooShort;
            }
            return ValidationResult.Success;
        }
    }
}
