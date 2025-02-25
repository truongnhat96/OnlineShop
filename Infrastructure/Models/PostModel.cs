using Entities;

namespace Infrastructure.Models
{
    public class PostModel
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Image = string.Empty;
        public IFormFile? ImageUrl { get; set; }
        public List<Post> Posts { get; set; } = [];
    }
}
