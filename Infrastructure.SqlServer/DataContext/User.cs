using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer.DataContext
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        [StringLength(100)]
        public required string Username { get; set; }
        [StringLength(100)]
        public required string Password { get; set; }
        [StringLength(100)]
        public required string DisplayName { get; set; }
        [StringLength(50)]
        public required string Email { get; set; }
        public int RoleId { get; set; }
        public virtual Role? Role { get; set; }
        public virtual ICollection<Post> Posts { get; set; } = [];
        public virtual ICollection<Review> Reviews { get; set; } = [];
        public virtual ICollection<CartItem> CartItems { get; set; } = [];
    }
}
