using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Models
{
    public class SignupModel
    {
        public required string Username { get; set; }    
        public required string Email { get; set; }
        public required string DisplayName { get; set; }

        [MinLength(5, ErrorMessage = "Mật khẩu phải có ít nhất 5 ký tự")]
        public required string Password { get; set; }
        public required string ConfirmPassword { get; set; }
    }
}
