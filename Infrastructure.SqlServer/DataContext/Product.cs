using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer.DataContext
{
    public class Product
    {
        [Key]
        public int Id { get; set; }
        [StringLength(100)]
        public required string Name { get; set; }
        [StringLength(50)]
        public required string Brand { get; set; }
        public int Quantity { get; set; }
        public DateTime Date_Import { get; set; }
        public double Price { get; set; }
        public int Sold { get; set; }
        public string? Coupon { get; set; }
        public int CategoryId { get; set; }
        [StringLength(10000000)]
        public required string Description { get; set; }
        [Url]
        public required string ImageUrl { get; set; }
        public virtual Category? Category { get; set; }
        public virtual ItemInfor? ItemInfor { get; set; }
        public virtual ICollection<CartItem> CartItems { get; set; } = [];
        public virtual ICollection<Review> Reviews { get; set; } = [];
    }
}
