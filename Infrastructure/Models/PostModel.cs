using Entities;

namespace Infrastructure.Models
{
    public class PostModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập tiêu đề bài viết.")]
        [System.ComponentModel.DataAnnotations.StringLength(100, ErrorMessage = "Tiêu đề bài viết tối đa 100 ký tự.")]
        public string Title { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập nội dung bài viết.")]
        public string Content { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Image = string.Empty;
        public IFormFile? ImageUrl { get; set; }
        public List<Post> Posts { get; set; } = [];
    }
}
