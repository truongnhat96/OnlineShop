using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer.DataContext
{
    public class Post
    {
        [Key]
        public Guid Id { get; set; }
        public required string Title { get; set; }
        public required string Content { get; set; }
        [Url]
        public required string ImageUrl { get; set; }
        public DateTime Date { get; set; }
        public int CategoryId { get; set; }
        public int UserId { get; set; }
        public virtual Category? Category { get; set; }
        public virtual User? User { get; set; }
    }
}
