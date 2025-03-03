namespace Infrastructure.Models
{
    public class LoginModel
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
        public bool IsRememberLoginInfor { get; set; }
    }
}
