using Entities;

namespace Infrastructure.Models
{
    public class SearchResultModel
    {
        public string Keyword { get; set; } = string.Empty;
        public List<Product> Products { get; set; } = [];
        public List<Post> Posts { get; set; } = [];
        public int currentPage { get; set; }
        public int totalPage { get; set; }
    }
}
