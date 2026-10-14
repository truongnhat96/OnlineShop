using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer.DataContext
{
    public class Category
    {
        [Key]
        public int Id { get; set; }
        [StringLength(100)]
        public required string Name { get; set; }
        public virtual ICollection<Product> Products { get; set; } = [];
        public virtual ICollection<Post> Posts { get; set; } = [];
    }
}
