using Entities;

namespace Infrastructure.Models
{
    public class ProductModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int Sold { get; set; }
        public DateTime Date_Import { get; set; }
        public double Price { get; set; }
        public double oldPrice { get; set; }
        public string? Coupon { get; set; }
        public int Discount { get; set; }
        public int CategoryId { get; set; } 
        public string CategoryName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public IFormFile? ImageUrl { get; set; }
        public List<Product> ProductList { get; set; } = [];
        public List<Category> CategoryList { get; set; } = [];
        public List<int> ProductListReview { get; set; } = [];
        public int Rating { get; set; }
        public int currentPage { get; set; }
        public int totalPage { get; set; }
        public int totalProduct { get; set; }
        public List<string> ReviewerName { get; set; } = [];
        public List<Review> Reviews { get; set; } = [];
        [System.ComponentModel.DataAnnotations.StringLength(100)] public string? ManufacturerCode { get; set; }
        [System.ComponentModel.DataAnnotations.StringLength(2048), System.ComponentModel.DataAnnotations.Url] public string? ReferenceUrl { get; set; }
        [System.ComponentModel.DataAnnotations.StringLength(2000)] public string? RecommendationTags { get; set; }
        [System.ComponentModel.DataAnnotations.StringLength(500)] public string? RecommendationUseCases { get; set; }
        public bool IsSampleData { get; set; }
        [System.ComponentModel.DataAnnotations.Range(0, 2)] public int FitmentKind { get; set; }
    }
}
