using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer.DataContext
{
    public class Role
    {
        [Key]
        public int Id { get; set; }
        [StringLength(20)]
        public required string Name { get; set; }
        public virtual ICollection<User> Users { get; set; } = [];
    }
}
