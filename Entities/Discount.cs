using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities
{
    public class Discount
    {
        public int Id { get; set; }
        public required string Coupon { get; set; }
        public int DiscountPercent { get; set; }
    }
}
