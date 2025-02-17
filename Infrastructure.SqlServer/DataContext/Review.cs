using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer.DataContext
{
    public class Review
    {
        [Key]
        public Guid Id { get; set; }
        public int Rating { get; set; }
        [StringLength(1000)]
        public string? Comment { get; set; }
        public int ProductId { get; set; }
        public DateTime CreatedAt { get; set; }
        public int UserId { get; set; }
        public virtual Product? Product { get; set; }
        public virtual User? User { get; set; }
    }
}
