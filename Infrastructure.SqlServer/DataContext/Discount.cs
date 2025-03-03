using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer.DataContext
{
    public class Discount
    {
        //ID is ProductID
        [Key]
        public int Id { get; set; }
        public required string Coupon { get; set; }
        public int DiscountPercent { get; set; }
        public virtual Product? Product { get; set; }
        public virtual ICollection<DiscountUsage> DiscountUsages { get; set; } = [];
    }
}
