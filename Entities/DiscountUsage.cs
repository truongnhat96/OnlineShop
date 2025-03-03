using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities
{
    public class DiscountUsage
    {
        public Guid Id { get; set; }
        public int UserId { get; set; }
        public int DiscountId { get; set; }
    }
}
