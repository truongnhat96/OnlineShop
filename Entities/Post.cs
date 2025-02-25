using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities
{
    public class Post
    {
        public Guid Id { get; set; }
        public required string Title { get; set; }
        public required string Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string ImageUrl { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int CategoryId { get; set; } = 10;
    }
}
