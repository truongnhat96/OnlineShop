using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities
{
    public class Product
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Brand { get; set; }
        public int Quantity { get; set; }
        public DateTime Date_Import { get; set; }
        public double Price { get; set; }
        public int Sold { get; set; }
        public string? Coupon { get; set; }
        public int CategoryId { get; set; }
        public required string Description { get; set; }
        public string? ImageUrl { get; set; }
        public double OldPrice {  get; set; }
    }
}
