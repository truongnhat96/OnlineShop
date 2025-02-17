namespace Entities
{
    public class User
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string DisplayName { get; set; }
        public required string Email { get; set; }
        public int RoleId { get; set; }
    }
}
